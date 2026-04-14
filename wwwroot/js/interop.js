window.animeTracker = {
    saveTextFile: function (fileName, content) {
        const blob = new Blob([content], { type: 'text/csv;charset=utf-8;' });
        const link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = fileName;
        link.style.display = 'none';
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    },

    setLocalData: function (key, value) {
        try {
            localStorage.setItem(key, value);
        } catch (error) {
            console.warn('Local storage unavailable.', error);
        }
    },

    getLocalData: function (key) {
        try {
            return localStorage.getItem(key);
        } catch (error) {
            console.warn('Local storage unavailable.', error);
            return null;
        }
    },

    capturePointer: function (element, pointerId) {
        if (!element || !element.setPointerCapture) {
            return;
        }

        try {
            element.setPointerCapture(pointerId);
        } catch (error) {
            console.warn('Pointer capture unavailable.', error);
        }
    },

    releasePointer: function (element, pointerId) {
        if (!element || !element.releasePointerCapture) {
            return;
        }

        try {
            element.releasePointerCapture(pointerId);
        } catch (error) {
            console.warn('Pointer release unavailable.', error);
        }
    }
};
