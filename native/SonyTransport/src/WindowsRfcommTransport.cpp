#include "sony/transport/WindowsRfcommTransport.h"

#include "sony/transport/BluetoothAddress.h"
#include "sony/transport/Logger.h"
#include "sony/transport/SonyError.h"

#include <winsock2.h>
#include <ws2bth.h>
#include <rpc.h>

#include <mutex>
#include <string>

#pragma comment(lib, "ws2_32.lib")
#pragma comment(lib, "rpcrt4.lib")

namespace sony::transport {

namespace {

constexpr auto kServiceUuidV1 = "96CC203E-5068-46ad-B32D-E316F5E069BA";
constexpr auto kServiceUuidV2 = "956C7B26-D49A-4BA8-B03F-B17D393CB6E2";
constexpr DWORD kReceiveTimeoutMs = 2500;
constexpr uintptr_t kInvalidSocket = static_cast<uintptr_t>(INVALID_SOCKET);

void ensureWinsock() {
    static std::once_flag once;
    std::call_once(once, [] {
        WSADATA data{};
        const int result = ::WSAStartup(MAKEWORD(2, 2), &data);
        if (result != 0) {
            throw SonyException(SonyErrorCode::TransportFailure, "WSAStartup failed: " + std::to_string(result));
        }
    });
}

void setSocketOption(SOCKET socket, int level, int name, DWORD value) {
    if (::setsockopt(socket, level, name, reinterpret_cast<const char*>(&value), sizeof(value)) == SOCKET_ERROR) {
        const int error = ::WSAGetLastError();
        ::closesocket(socket);
        throw SonyException(SonyErrorCode::TransportFailure, "setsockopt failed: " + std::to_string(error));
    }
}

SOCKET createSocket() {
    const SOCKET socket = ::socket(AF_BTH, SOCK_STREAM, BTHPROTO_RFCOMM);
    if (socket == INVALID_SOCKET) {
        throw SonyException(SonyErrorCode::TransportFailure, "Couldn't create Bluetooth socket: " + std::to_string(::WSAGetLastError()));
    }
    setSocketOption(socket, SOL_RFCOMM, SO_BTH_AUTHENTICATE, TRUE);
    setSocketOption(socket, SOL_RFCOMM, SO_BTH_ENCRYPT, TRUE);
    setSocketOption(socket, SOL_SOCKET, SO_RCVTIMEO, kReceiveTimeoutMs);
    return socket;
}

} // namespace

WindowsRfcommTransport::WindowsRfcommTransport() noexcept
    : _socket(kInvalidSocket) {}

WindowsRfcommTransport::~WindowsRfcommTransport() {
    disconnect();
}

void WindowsRfcommTransport::connect(const DeviceAddress& address) {
    disconnect();

    const auto parsed = parseBluetoothAddress(address.str());
    if (!parsed) {
        throw SonyException(SonyErrorCode::TransportFailure, "Invalid Bluetooth address: " + address.str());
    }
    ensureWinsock();

    // Same order as upstream: V1 service first, then the V2 service.
    if (tryConnect(*parsed, kServiceUuidV1) == 0) {
        Logger::info(LogCategory::Transport, "RFCOMM connected on the V1 service");
        return;
    }
    const int error = tryConnect(*parsed, kServiceUuidV2);
    if (error == 0) {
        Logger::info(LogCategory::Transport, "RFCOMM connected on the V2 service");
        return;
    }
    throw SonyException(SonyErrorCode::TransportFailure, "Couldn't connect to " + address.str() + " (Winsock error " + std::to_string(error) + ")");
}

int WindowsRfcommTransport::tryConnect(uint64_t address, const char* serviceUuid) {
    const SOCKET socket = createSocket();

    // SOCKADDR_BTH is packed, so parse the GUID into an aligned local first.
    GUID serviceClassId{};
    if (::UuidFromStringA(reinterpret_cast<RPC_CSTR>(const_cast<char*>(serviceUuid)), &serviceClassId) != RPC_S_OK) {
        ::closesocket(socket);
        throw SonyException(SonyErrorCode::TransportFailure, std::string("Invalid service UUID ") + serviceUuid);
    }

    SOCKADDR_BTH target{};
    target.addressFamily = AF_BTH;
    target.btAddr = address;
    target.serviceClassId = serviceClassId;

    if (::connect(socket, reinterpret_cast<const sockaddr*>(&target), sizeof(target)) == SOCKET_ERROR) {
        const int error = ::WSAGetLastError();
        ::closesocket(socket);
        Logger::debug(LogCategory::Transport, std::string("RFCOMM connect to ") + serviceUuid + " failed: " + std::to_string(error));
        return error;
    }

    // Never leak a socket a racing connect left behind.
    const uintptr_t previous = _socket.exchange(static_cast<uintptr_t>(socket));
    if (previous != kInvalidSocket) {
        ::closesocket(static_cast<SOCKET>(previous));
    }
    _connected.store(true);
    return 0;
}

void WindowsRfcommTransport::disconnect() noexcept {
    _connected.store(false);
    const uintptr_t socket = _socket.exchange(kInvalidSocket);
    if (socket != kInvalidSocket) {
        ::shutdown(static_cast<SOCKET>(socket), SD_BOTH);
        ::closesocket(static_cast<SOCKET>(socket));
    }
}

bool WindowsRfcommTransport::isConnected() const noexcept {
    return _connected.load();
}

size_t WindowsRfcommTransport::send(std::span<const std::byte> data) {
    const uintptr_t socket = _socket.load();
    if (!_connected.load() || socket == kInvalidSocket) {
        throw SonyException(SonyErrorCode::Disconnected, "Transport not connected");
    }
    const int sent = ::send(static_cast<SOCKET>(socket), reinterpret_cast<const char*>(data.data()), static_cast<int>(data.size()), 0);
    if (sent == SOCKET_ERROR) {
        const int error = ::WSAGetLastError();
        _connected.store(false);
        throw SonyException(SonyErrorCode::Disconnected, "Bluetooth send failed: " + std::to_string(error));
    }
    return static_cast<size_t>(sent);
}

size_t WindowsRfcommTransport::receive(std::span<std::byte> buffer) {
    const uintptr_t socket = _socket.load();
    if (!_connected.load() || socket == kInvalidSocket) {
        throw SonyException(SonyErrorCode::Disconnected, "Transport not connected");
    }
    const int received = ::recv(static_cast<SOCKET>(socket), reinterpret_cast<char*>(buffer.data()), static_cast<int>(buffer.size()), 0);
    if (received == SOCKET_ERROR) {
        const int error = ::WSAGetLastError();
        if (error == WSAETIMEDOUT) {
            throw SonyException(SonyErrorCode::Timeout, "Receive timed out");
        }
        _connected.store(false);
        throw SonyException(SonyErrorCode::Disconnected, "Bluetooth receive failed: " + std::to_string(error));
    }
    if (received == 0) {
        _connected.store(false);
        throw SonyException(SonyErrorCode::Disconnected, "Headset closed the connection");
    }
    return static_cast<size_t>(received);
}

} // namespace sony::transport
