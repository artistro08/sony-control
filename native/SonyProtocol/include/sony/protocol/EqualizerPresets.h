#pragma once

// The single authoritative equalizer preset table.
//
// These byte values are reverse-engineered and were previously written out by
// hand in three places: the CLI, the IPC layer and the desktop controller. The
// controller's copy was off by one and had bass and treble swapped, so picking
// "Vocal" applied Relaxed, "Bright" fell through to Off, and the label under the
// chips named a different preset from the one in effect. Anything that needs to
// turn a preset into a name, or the reverse, uses this header.

#include <cstdint>
#include <string>
#include <string_view>
#include <vector>

namespace sony::protocol {

enum class EqualizerPreset : uint8_t {
    Off         = 0x00,
    Bright      = 0x10,
    Excited     = 0x11,
    Mellow      = 0x12,
    Relaxed     = 0x13,
    Vocal       = 0x14,
    TrebleBoost = 0x15,
    BassBoost   = 0x16,
    Speech      = 0x17,
    Manual      = 0xa0,
};

struct EqualizerPresetInfo {
    EqualizerPreset preset;
    std::string_view id;           ///< stable identifier used on the CLI and over IPC
    std::string_view displayName;  ///< human-readable label
};

/// Every preset, in the order they are offered in the interface.
[[nodiscard]] const std::vector<EqualizerPresetInfo>& equalizerPresets() noexcept;

/// Display name for a raw preset byte. Unknown values render as "Preset (n)".
[[nodiscard]] std::string equalizerPresetName(int preset);

/// Stable identifier for a raw preset byte, e.g. "bass-boost". Empty if unknown.
[[nodiscard]] std::string_view equalizerPresetId(int preset) noexcept;

/// Parse an identifier or display name, case-insensitively. Accepts a few
/// aliases ("bass", "treble") and a decimal number. Returns -1 when unknown.
[[nodiscard]] int equalizerPresetFromName(std::string_view name);

/// Sony's firmware keeps a custom curve in one of several memory slots, not just one:
/// 0xa0 (what the Sony Headphones Connect app calls "Custom 1") through 0xa5 ("Custom 6").
/// A device the Sony app was used on can come back with a custom curve stored in any of
/// them, but this app only ever writes and recognizes 0xa0 (Manual), so a headset with,
/// say, "Custom 3" active reported an unknown preset and the picker showed nothing even
/// though the bands were read correctly. Folding the whole range onto Manual here, at the
/// one place raw preset bytes turn into app state, is what fixes that for every reader.
[[nodiscard]] constexpr int normalizeEqualizerPreset(int raw) noexcept {
    constexpr int kCustomRangeStart = static_cast<int>(EqualizerPreset::Manual); // 0xa0
    constexpr int kCustomRangeEnd = kCustomRangeStart + 5;                       // 0xa5
    return (raw >= kCustomRangeStart && raw <= kCustomRangeEnd) ? kCustomRangeStart : raw;
}

} // namespace sony::protocol
