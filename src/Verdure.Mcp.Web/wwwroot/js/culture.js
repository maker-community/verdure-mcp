// Culture management for Blazor WebAssembly localization
window.blazorCulture = {
    get: function() {
        return localStorage.getItem('BlazorCulture');
    },
    set: function(value) {
        localStorage.setItem('BlazorCulture', value);
    }
};
