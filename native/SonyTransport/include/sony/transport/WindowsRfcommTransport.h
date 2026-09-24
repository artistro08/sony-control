#pragma once

#include "ITransport.h"

#include <atomic>
#include <cstdint>

namespace sony::transport {

// RFCOMM link to a Sony headset over Winsock (AF_BTH).
//
// Ported from upstream's WindowsBluetoothConnector: tries the V1 service UUID
// first, then the V2 UUID, requires authentication and encryption, and sets a
// 2.5 s receive timeout so the session reader can check its stop flag.
class WindowsRfcommTransport final : public ITransport {
public:
    WindowsRfcommTransport() noexcept;
    ~WindowsRfcommTransport() override;

    WindowsRfcommTransport(const WindowsRfcommTransport&) = delete;
    WindowsRfcommTransport& operator=(const WindowsRfcommTransport&) = delete;

    void connect(const DeviceAddress& address) override;
    void disconnect() noexcept override;
    [[nodiscard]] bool isConnected() const noexcept override;
    size_t send(std::span<const std::byte> data) override;
    size_t receive(std::span<std::byte> buffer) override;

private:
    // Returns 0 on success, otherwise the Winsock error code.
    [[nodiscard]] int tryConnect(uint64_t address, const char* serviceUuid);

    std::atomic<uintptr_t> _socket;
    std::atomic<bool> _connected{false};
};

} // namespace sony::transport
