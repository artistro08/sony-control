#pragma once

#include "SemanticTypes.h"
#include "sony/transport/SonyError.h"
#include <array>
#include <string>
#include <vector>

namespace sony::protocol {

enum class ProtocolGeneration {
    V1,
    V2
};

class IProtocol {
public:
    virtual ~IProtocol() = default;

    [[nodiscard]] virtual ProtocolGeneration generation() const noexcept = 0;

    virtual void initDevice() = 0;

    virtual BatteryState getBattery() = 0;

    virtual NoiseControlState getNoiseControl() = 0;
    virtual void setNoiseControl(const NoiseControlState& state) = 0;

    virtual EqualizerState getEqualizer() = 0;
    virtual void setEqualizerPreset(int preset) = 0;
    virtual void setEqualizerCustom(int clearBass, const std::array<int, 5>& bands) = 0;

    virtual bool getDsee() = 0;
    virtual void setDsee(bool enabled) = 0;

    virtual std::string getFirmwareVersion() = 0;
    virtual std::string getCodec() = 0;

    virtual int getAutoPowerOff() = 0;
    virtual void setAutoPowerOff(int index) = 0;
    // Turns the headset off. It may drop the link before acknowledging.
    virtual void powerOff() = 0;

    virtual bool getSpeakToChat() = 0;
    virtual void setSpeakToChat(bool enabled) = 0;

    virtual bool getAdaptiveVolume() = 0;
    virtual void setAdaptiveVolume(bool enabled) = 0;

    // Multipoint: devices connected to the headset and switching playback between them.
    // Generations without it throw Unsupported.
    virtual std::vector<PlaybackDevice> getPlaybackDevices() {
        throw SonyException(SonyErrorCode::Unsupported, "Playback switching isn't supported");
    }
    virtual void switchPlayback(const std::string& /*address*/) {
        throw SonyException(SonyErrorCode::Unsupported, "Playback switching isn't supported");
    }
};

} // namespace sony::protocol
