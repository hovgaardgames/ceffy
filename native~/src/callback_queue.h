#pragma once

#include <queue>
#include <mutex>
#include <string>

enum class CeffyCallbackType {
    ConsoleMessage = 1,
    JsMessage = 2,
    DragStart = 3,
    NativeLog = 4,
};

struct CeffyCallbackData {
    CeffyCallbackType type;
    int browserId;
    std::string data;
};

class CallbackQueue {
public:
    void Push(CeffyCallbackData item) {
        std::lock_guard<std::mutex> lock(mutex_);
        queue_.push(std::move(item));
    }

    bool TryPop(CeffyCallbackData& out) {
        std::lock_guard<std::mutex> lock(mutex_);
        if (queue_.empty()) return false;
        out = std::move(queue_.front());
        queue_.pop();
        return true;
    }

private:
    std::queue<CeffyCallbackData> queue_;
    std::mutex mutex_;
};
