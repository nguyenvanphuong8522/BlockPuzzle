// Phone vibration for the WebGL build (Vibration API: Chrome on Android; iOS Safari ignores it).
mergeInto(LibraryManager.library, {
    Survivor_Vibrate: function (milliseconds) {
        if (typeof navigator !== "undefined" && navigator.vibrate) navigator.vibrate(milliseconds);
    }
});
