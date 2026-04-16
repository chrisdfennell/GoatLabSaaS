// GoatLab Weather — geolocation helper
window.goatWeather = {
    getLocation: function () {
        return new Promise((resolve) => {
            if (!navigator.geolocation) { resolve(null); return; }
            navigator.geolocation.getCurrentPosition(
                (pos) => resolve({ lat: pos.coords.latitude, lng: pos.coords.longitude }),
                (_err) => resolve(null),
                { timeout: 10000, enableHighAccuracy: false, maximumAge: 15 * 60 * 1000 }
            );
        });
    }
};
