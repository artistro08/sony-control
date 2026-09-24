#include "sony/transport/BluetoothAddress.h"

namespace sony::transport {

namespace {

[[nodiscard]] constexpr int hexValue(char c) noexcept {
    if (c >= '0' && c <= '9') {
        return c - '0';
    }
    if (c >= 'a' && c <= 'f') {
        return c - 'a' + 10;
    }
    if (c >= 'A' && c <= 'F') {
        return c - 'A' + 10;
    }
    return -1;
}

} // namespace

std::optional<uint64_t> parseBluetoothAddress(std::string_view text) noexcept {
    constexpr int kDigits = 12;

    uint64_t value = 0;
    int digits = 0;
    for (const char c : text) {
        if (c == ':' || c == '-') {
            continue;
        }
        const int nibble = hexValue(c);
        if (nibble < 0 || digits == kDigits) {
            return std::nullopt;
        }
        value = (value << 4) | static_cast<uint64_t>(nibble);
        ++digits;
    }

    if (digits != kDigits) {
        return std::nullopt;
    }
    return value;
}

} // namespace sony::transport
