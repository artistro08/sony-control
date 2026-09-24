#pragma once

#include <cstdint>
#include <optional>
#include <string_view>

namespace sony::transport {

// Parses "AA:BB:CC:DD:EE:FF", "AA-BB-CC-DD-EE-FF" or "AABBCCDDEEFF" (any case)
// into the 48-bit value Winsock expects in SOCKADDR_BTH::btAddr.
[[nodiscard]] std::optional<uint64_t> parseBluetoothAddress(std::string_view text) noexcept;

} // namespace sony::transport
