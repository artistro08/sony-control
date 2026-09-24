#pragma once

#include "SemanticTypes.h"

#include <cstdint>
#include <span>

namespace sony::protocol {

// Newer V2 layouts a real WF-1000XM6 uses that upstream's WH-1000XM5-era code doesn't
// know. Byte meanings from mos9527/SonyHeadphonesClient (ProtocolV2T1.hpp), confirmed
// against replies captured from a WF-1000XM6 on firmware 1.6.0.

inline constexpr uint8_t kNcAsmSeamless = 0x19;

// Noise control type 0x19, same layout for GET reply (0x67), SET (0x68) and notify (0x69):
//   <op> 19 <status: 01 final> <effect: 00 off, 01 on> <mode: 00 NC, 01 ambient>
//        <voice: 01 focus on voice> <ambient level 0-20> <auto ambient> <adaptive sensitivity>
// Returns false when the payload isn't that layout.
inline bool parseNcAsmSeamless(std::span<const uint8_t> payload, NoiseControlState& state) noexcept {
    if (payload.size() < 9 || payload[1] != kNcAsmSeamless) {
        return false;
    }
    const bool on = payload[3] != 0;
    const bool ambient = payload[4] == 1;

    if (!on) {
        state.mode = NoiseControlMode::Off;
    } else if (ambient) {
        state.mode = NoiseControlMode::Ambient;
    } else {
        state.mode = NoiseControlMode::NoiseCancelling;
    }
    state.ambientLevel = state.mode == NoiseControlMode::Ambient ? static_cast<int>(payload[6]) : 0;
    state.focusOnVoice = payload[5] == 1;
    return true;
}

// Equalizer reply or notify: <op> 00 <preset> <band count> <bands...>
// Six bands are Clear Bass then 400 Hz-16 kHz, each dB + 10. Other counts (a ten-band
// WF-1000XM6 sends 10, each step + 6) keep only the preset; the five-band curve stays empty.
// Returns false when the payload isn't an equalizer message.
inline bool parseEqualizer(std::span<const uint8_t> payload, EqualizerState& state) noexcept {
    if (payload.size() < 4 || payload[1] != 0x00) {
        return false;
    }
    state.preset = static_cast<int>(payload[2]);
    state.clearBass = 0;
    state.bands = {0, 0, 0, 0, 0};

    if (payload[3] == 6 && payload.size() >= 10) {
        state.clearBass = static_cast<int>(payload[4]) - 10;
        for (size_t i = 0; i < state.bands.size(); ++i) {
            state.bands[i] = static_cast<int>(payload[5 + i]) - 10;
        }
    }
    return true;
}

} // namespace sony::protocol
