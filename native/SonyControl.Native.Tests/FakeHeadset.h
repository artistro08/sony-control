#pragma once

#include "sony/protocol/FrameCodec.h"
#include "sony/protocol/SonyFrame.h"
#include "sony/transport/FakeTransport.h"

#include <chrono>
#include <cstdint>
#include <deque>
#include <initializer_list>
#include <mutex>
#include <span>
#include <thread>
#include <vector>

namespace sony::test {

using Payload = std::vector<uint8_t>;

// Scripted headset on top of upstream's FakeTransport.
//
// Each queued reply answers one host request, in order, at the moment the host
// writes it: an ACK carrying the next expected sequence number, then any data
// frames. Device data frames alternate sequence 0/1 like real hardware so the
// session doesn't drop them as duplicates. ACKs the host sends back never
// consume a reply.
class FakeHeadset : public transport::FakeTransport {
public:
    // Answer the next request with an ACK followed by these data payloads.
    void reply(std::initializer_list<Payload> payloads = {}) {
        std::lock_guard lock(_scriptMutex);
        _replies.push_back(Reply{true, std::vector<Payload>(payloads)});
    }

    // Leave the next request unanswered: no ACK, no data.
    void ignore() {
        std::lock_guard lock(_scriptMutex);
        _replies.push_back(Reply{false, {}});
    }

    // Send an unsolicited data frame right now.
    void notify(const Payload& payload) {
        queueIncoming(encodeData(payload));
    }

    // Send the same data frame twice with the same sequence number.
    void notifyTwice(const Payload& payload) {
        const auto frame = encodeData(payload);
        queueIncoming(frame);
        queueIncoming(frame);
    }

    // Payloads of every DataMdr request the host sent, in order.
    [[nodiscard]] std::vector<Payload> requests() const {
        std::lock_guard lock(_scriptMutex);
        return _requests;
    }

    // Sequence numbers of every DataMdr request the host sent, in order.
    [[nodiscard]] std::vector<uint8_t> requestSequences() const {
        std::lock_guard lock(_scriptMutex);
        return _requestSequences;
    }

    size_t send(std::span<const std::byte> data) override {
        const size_t written = transport::FakeTransport::send(data);

        protocol::SonyFrame request;
        try {
            std::vector<uint8_t> bytes;
            bytes.reserve(data.size());
            for (const auto b : data) {
                bytes.push_back(static_cast<uint8_t>(b));
            }
            request = protocol::FrameCodec::decode(bytes);
        } catch (...) {
            return written;
        }
        if (request.type != protocol::DataType::DataMdr) {
            return written;
        }

        Reply reply;
        bool hasReply = false;
        {
            std::lock_guard lock(_scriptMutex);
            _requests.push_back(request.payload);
            _requestSequences.push_back(request.sequence);
            if (!_replies.empty()) {
                reply = std::move(_replies.front());
                _replies.pop_front();
                hasReply = true;
            }
        }
        if (!hasReply || !reply.ack) {
            return written;
        }

        std::vector<uint8_t> incoming = protocol::FrameCodec::encode(protocol::SonyFrame{
            .type = protocol::DataType::Ack,
            .sequence = static_cast<uint8_t>(1 - (request.sequence & 1)),
            .payload = {},
        });
        for (const auto& payload : reply.payloads) {
            const auto frame = encodeData(payload);
            incoming.insert(incoming.end(), frame.begin(), frame.end());
        }
        queueIncoming(incoming);
        return written;
    }

private:
    struct Reply {
        bool ack{true};
        std::vector<Payload> payloads;
    };

    std::vector<uint8_t> encodeData(const Payload& payload) {
        uint8_t sequence = 0;
        {
            std::lock_guard lock(_scriptMutex);
            sequence = _deviceSequence;
            _deviceSequence ^= 1;
        }
        return protocol::FrameCodec::encode(protocol::SonyFrame{
            .type = protocol::DataType::DataMdr,
            .sequence = sequence,
            .payload = payload,
        });
    }

    mutable std::mutex _scriptMutex;
    std::deque<Reply> _replies;
    std::vector<Payload> _requests;
    std::vector<uint8_t> _requestSequences;
    uint8_t _deviceSequence{0};
};

// Polls until the predicate holds or the timeout passes.
template <typename Predicate>
bool waitUntil(Predicate predicate, std::chrono::milliseconds timeout = std::chrono::milliseconds(3000)) {
    const auto deadline = std::chrono::steady_clock::now() + timeout;
    while (std::chrono::steady_clock::now() < deadline) {
        if (predicate()) {
            return true;
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(5));
    }
    return predicate();
}

inline constexpr const char* kTestAddress = "AC:80:0A:12:34:56";

// Replies for everything HeadsetController::connect asks a WF-1000XM6. Layouts are the
// ones a real WF-1000XM6 (firmware 1.6.0) sent; only the values differ.
inline void scriptXm6Connect(FakeHeadset& headset, bool answerHandshake = true) {
    if (answerHandshake) {
        headset.reply({{0x01, 0x00, 0x03, 0x00, 0x30, 0x02, 0x00, 0x00}});          // 00 00 handshake
    } else {
        headset.reply();                                                            // 00 00 handshake, ACK only
    }
    headset.reply({{0x23, 0x09, 85, 0x00, 82, 0x00, 0x64, 0x64}});                  // 22 09 left 85, right 82
    headset.reply({{0x23, 0x0a, 95, 0x00, 0x1e}});                                  // 22 0a case 95
    headset.reply({{0x67, 0x19, 0x01, 0x01, 0x01, 0x01, 0x08, 0x00, 0x00}});        // 66 19 ambient 8, voice on
    headset.reply({{0x57, 0x00, 0x10, 0x0a, 0x06, 0x06, 0x07, 0x08, 0x06, 0x05, 0x06, 0x06, 0x06, 0x06}}); // 56 00 Bright, 10 bands
    headset.reply({{0xe7, 0x01, 0x01}});                                            // e6 01 DSEE on
    headset.reply({{0xf7, 0x0c, 0x00, 0x01}});                                      // f6 0c Speak-to-Chat on (inverted)
    headset.reply({{0x27, 0x05, 0x10, 0x00}});                                      // 26 05 when taken off (index 5)
    headset.reply({{0x05, 0x02, 0x05, '1', '.', '6', '.', '0'}});                   // 04 02 firmware 1.6.0
    headset.reply({{0x13, 0x02, 0x10}});                                            // 12 02 LDAC
}

// Replies for everything HeadsetController::connect asks a WH-1000XM4.
inline void scriptXm4Connect(FakeHeadset& headset) {
    headset.reply({{0x67, 0x02, 0x11, 0x01, 0x02, 0x01, 0x00, 0x00}});             // 66 02 init poll: noise cancelling
    headset.reply({{0x11, 0x00, 60, 0x00}});                                        // 10 00 battery 60
    headset.reply({{0x67, 0x02, 0x11, 0x01, 0x00, 0x01, 0x01, 0x0f}});             // 66 02 ambient 15, voice on
    headset.reply({{0x57, 0x01, 0x16, 0x06, 0x0c, 0x0a, 0x0a, 0x0a, 0x0a, 0x0a}}); // 56 01 Bass Boost, Clear Bass +2
    headset.reply({{0x05, 0x02, 0x05, '3', '.', '0', '.', '1'}});                   // 04 02 firmware 3.0.1
    headset.reply({{0x19, 0x00, 0x02}});                                            // 18 00 AAC
}

} // namespace sony::test
