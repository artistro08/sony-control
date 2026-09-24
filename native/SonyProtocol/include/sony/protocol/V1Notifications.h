#pragma once

#include "DeviceState.h"

#include <cstdint>
#include <span>

namespace sony::protocol {

// Applies a protocol V1 (WH-1000XM4 era) notification or reply to the state.
//
// Upstream's DeviceEventDispatcher only understands V2 layouts, so V1 devices
// route their notifications here instead. Recognized payloads:
//   11/13 00 <level> <charging>                         battery
//   67/69 02 <effect> <nc> <dualSingle> <asm> <voice> <level>  noise control
//   57/59 01 <preset> 06 <bass+10> <b1..b5 +10>          equalizer
// Returns true when the payload was recognized and the state changed.
bool applyV1Notification(std::span<const uint8_t> payload, DeviceState& state);

} // namespace sony::protocol
