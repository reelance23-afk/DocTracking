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
            try
            {
                SelectedTheme = await _js.InvokeAsync<string>("localStorage.getItem", "theme") ?? "Default";
                var dark = await _js.InvokeAsync<string>("localStorage.getItem", "darkMode");
                IsDarkMode = dark == "true";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ThemeService] LoadAsync error: {ex.Message}");
            }
            OnChange?.Invoke();
        }

        public async Task SetThemeAsync(string theme)
        {
            SelectedTheme = theme;
            try
            {
                await _js.InvokeVoidAsync("localStorage.setItem", "theme", theme);
            }
            catch (Exception ex) { Console.WriteLine($"[ThemeService] SetTheme error: {ex.Message}"); }
            OnChange?.Invoke();
        }

        public async Task SetDarkModeAsync(bool isDark)
        {
            IsDarkMode = isDark;
            try
            {
                await _js.InvokeVoidAsync("localStorage.setItem", "darkMode", isDark.ToString().ToLower());
            }
            catch (Exception ex) { Console.WriteLine($"[ThemeService] SetDarkMode error: {ex.Message}"); }
            OnChange?.Invoke();
        }
    }
}