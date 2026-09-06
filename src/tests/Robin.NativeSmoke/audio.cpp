#include <windows.h>
#include <stdint.h>
static volatile LONG stopped = 0;
static volatile LONG lastPlaying = 0;
extern "C" __declspec(noinline) unsigned PostEvent(unsigned eventId, uint64_t objectId) {
    if (eventId == 99) {
        auto volatile nested = &PostEvent;
        nested(42, objectId);
    }
    return eventId + 1000;
}
extern "C" void ExecuteAction(int action, unsigned playingId, int, int) {
    // Independently pinned to Audiokinetic's AkSoundEngine.h: Stop=0, Pause=1.
    // https://github.com/audiokinetic/WwiseIncludes/blob/master/SDK/include/AK/SoundEngine/Common/AkSoundEngine.h
    if (action == 0) { InterlockedIncrement(&stopped); InterlockedExchange(&lastPlaying, playingId); }
}
extern "C" __declspec(dllexport) LONG StopCount() { return stopped; }
extern "C" __declspec(dllexport) LONG LastPlaying() { return lastPlaying; }
