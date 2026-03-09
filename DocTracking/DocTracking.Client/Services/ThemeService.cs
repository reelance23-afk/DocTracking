using Microsoft.JSInterop;

namespace DocTracking.Client.Services
{
    public class ThemeService
    {
        private readonly IJSRuntime _js;
        public string SelectedTheme { get; private set; } = "Default";
        public bool IsDarkMode { get; private set; } = false;
        public event Action? OnChange;

        public ThemeService(IJSRuntime js) => _js = js;

        public async Task LoadAsync()
        {
            SelectedTheme = await _js.InvokeAsync<string>("localStorage.getItem", "theme") ?? "Default";
            var dark = await _js.InvokeAsync<string>("localStorage.getItem", "darkMode");
            IsDarkMode = dark == "true";
            OnChange?.Invoke();
        }

        public async Task SetThemeAsync(string theme)
        {
            SelectedTheme = theme;
            await _js.InvokeVoidAsync("localStorage.setItem", "theme", theme);
            OnChange?.Invoke();
        }

        public async Task SetDarkModeAsync(bool isDark)
        {
            IsDarkMode = isDark;
            await _js.InvokeVoidAsync("localStorage.setItem", "darkMode", isDark.ToString().ToLower());
            OnChange?.Invoke();
        }
    }
}