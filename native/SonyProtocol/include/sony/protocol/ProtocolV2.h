#pragma once

#include "IProtocol.h"
#include "SonyProtocolSession.h"
#include <mutex>

namespace sony::protocol {

class ProtocolV2 : public IProtocol {
public:
    // earbuds: ask for left/right and case battery before the single-battery query.
    explicit ProtocolV2(SonyProtocolSession& session, bool earbuds = false);
    ~ProtocolV2() override = default;

    [[nodiscard]] ProtocolGeneration generation() const noexcept override {
        return ProtocolGeneration::V2;
    }

    void initDevice() override;

    // V2 uses opcode 0x22 for battery inquiries
    BatteryState getBattery() override;

    NoiseControlState getNoiseControl() override;
    void setNoiseControl(const NoiseControlState& state) override;

    EqualizerState getEqualizer() override;
    void setEqualizerPreset(int preset) override;
    void setEqualizerCustom(int clearBass, const std::array<int, 5>& bands) override;

    bool getDsee() override;
    void setDsee(bool enabled) override;
    void powerOff() override;

    std::string getFirmwareVersion() override;
    std::string getCodec() override;

    int getAutoPowerOff() override;
    void setAutoPowerOff(int index) override;

    bool getSpeakToChat() override;
    void setSpeakToChat(bool enabled) override;

    bool getAdaptiveVolume() override;
    void setAdaptiveVolume(bool enabled) override;

private:
    SonyProtocolSession& _session;
    std::mutex _mutex;
    bool _earbuds{false};
    // Noise control type the headset answered: 0x19 (WF-1000XM6) or 0x17 (upstream). 0 = not read yet.
    uint8_t _ncAsmType{0};
    int _lastAmbientLevel{10};
};

} // namespace sony::protocol
