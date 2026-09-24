#pragma once

#include "SonyError.h"

#include <cstdint>

namespace sony::protocol {

// HRESULT reported across the WinRT boundary for each error code. The C# side
// turns these back into user-facing messages (HeadsetErrorMessages).
[[nodiscard]] constexpr int32_t toHresult(SonyErrorCode code) noexcept {
    switch (code) {
        case SonyErrorCode::Timeout:
            return static_cast<int32_t>(0x800705B4); // HRESULT_FROM_WIN32(ERROR_TIMEOUT)
        case SonyErrorCode::Disconnected:
            return static_cast<int32_t>(0x8007048F); // HRESULT_FROM_WIN32(ERROR_DEVICE_NOT_CONNECTED)
        case SonyErrorCode::Unsupported:
            return static_cast<int32_t>(0x80004001); // E_NOTIMPL
        case SonyErrorCode::InvalidFrame:
        case SonyErrorCode::InvalidChecksum:
        case SonyErrorCode::InvalidResponse:
        case SonyErrorCode::ProtocolViolation:
            return static_cast<int32_t>(0x8007000D); // HRESULT_FROM_WIN32(ERROR_INVALID_DATA)
        case SonyErrorCode::TransportFailure:
            return static_cast<int32_t>(0x800704C9); // HRESULT_FROM_WIN32(ERROR_CONNECTION_REFUSED)
    }
    return static_cast<int32_t>(0x80004005); // E_FAIL
}

} // namespace sony::protocol
