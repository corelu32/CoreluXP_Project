using SDL;
using static SDL.SDL3;

namespace SDL3Demo;

public sealed unsafe class Window : IDisposable
{
    private SDL_Window* _window = null;
    private SDL_Renderer* _renderer = null;

    public event Action? OnStart;
    public event Action? OnUpdate;

    public Window(string name, int width, int height)
    {
        // Initialize SDL. Ensure it doesn't fail.
        if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO))
            throw new Exception($"Failed to initialize SDL3. {SDL_GetError()}");

        // Create the window handle. Ensure it doesn't fail.
        //      Hide the window until the Run() method is called.
        //      This prevents an "unprepared" window from showing briefly during startup.
        _window = SDL_CreateWindow(name, width, height, SDL_WindowFlags.SDL_WINDOW_HIDDEN);

        if (_window is null)
            throw new Exception($"Failed to create the window. {SDL_GetError()}");
        
        // Create the renderer handle. Ensure it doesn't fail.
        _renderer = SDL_CreateRenderer(_window, (byte*)null);

        if (_renderer is null)
            throw new Exception($"Failed to create the renderer. {SDL_GetError()}");
    }

    public void Run()
    {
        SDL_Event sdlEvent;
        bool running = true;

        OnStart?.Invoke();
        SDL_ShowWindow(_window);

        SDL_FRect rect = new()
        {
            x = 300,
            y = 150,
            w = 200,
            h = 200
        };

        while (running)
        {
            while (SDL_PollEvent(&sdlEvent))
            {
                switch ((SDL_EventType)sdlEvent.type)
                {
                    case SDL_EventType.SDL_EVENT_QUIT:
                        running = false;
                        break;
                    
                    case SDL_EventType.SDL_EVENT_KEY_DOWN:
                        if (sdlEvent.key.key == SDL_Keycode.SDLK_ESCAPE)
                        {
                            running = false;
                        }
                        break;
                }
            }

            SDL_SetRenderDrawColor(_renderer, 30, 30, 30, 255);
            SDL_RenderClear(_renderer);

            OnUpdate?.Invoke();

            SDL_SetRenderDrawColor(_renderer, 255, 255, 255, 50);
            SDL_RenderRect(_renderer, &rect);
            SDL_RenderPresent(_renderer);
        }
    }

    public void Dispose()
    {
        SDL_DestroyRenderer(_renderer);
        SDL_DestroyWindow(_window);
        SDL_Quit();
    }
}