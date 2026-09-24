#pragma once

#include <array>
#include <cstdint>
#include <optional>
#include <string>

namespace sony::protocol {

struct BatteryState {
    std::optional<int> main;
    std::optional<int> left;
    std::optional<int> right;
    std::optional<int> caseBattery;
    bool charging{false};
};

enum class NoiseControlMode {
    Off,
    NoiseCancelling,
    Ambient
};

struct NoiseControlState {
    NoiseControlMode mode{NoiseControlMode::Off};
    int ambientLevel{0};
    bool focusOnVoice{false};
};

// A device (PC, phone) connected to the headset over Bluetooth, for multipoint switching.
struct PlaybackDevice {
    std::string address;   // "AA:BB:CC:DD:EE:FF", as the headset reports it
    std::string name;      // Bluetooth name the headset knows it by
    bool playing{false};   // has the audio right now

    bool operator==(const PlaybackDevice&) const = default;
};

struct EqualizerState {
    int preset{0};
    int clearBass{0};
    std::array<int, 5> bands{0, 0, 0, 0, 0};
};

} // namespace sony::protocol
