using static SDL.SDL3;
using SDL;
using Tot3Dx.Primitives;

namespace Tot3Dx.Applications
{
    /// <summary>
    ///   Options to initialize an application window.
    /// </summary>

    public struct DesktopApplicationOptions
    {
        public required string Title;
        public required int    Width;
        public required int    Height;

        public DesktopApplicationOptions() { }
    }

    /// <summary>
    ///   An application window for rendering graphics and event handling.
    /// </summary>

    public unsafe sealed class DesktopApplication : IApplication
    {
        private readonly SDL_Window* _window = null;

        public DesktopApplicationOptions Options { get; private set; }
        public bool Running { get; private set; }

        public Action?          OnStart   { private get; set; }
        public Action?          OnQuit    { private get; set; }
        public Action<KeyCode>? OnKeyDown { private get; set; }
        public Action<float>?   OnUpdate  { private get; set; }

        /// <summary>
        ///   Initializes SDL and creates the window.
        /// </summary>

        public DesktopApplication(DesktopApplicationOptions options)
        {
            Options = options;
            Running = true;

            // Initialize SDL. Ensure it doesn't fail.
            if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO))
                throw new ApplicationException($"Failed to initialize SDL3. {SDL_GetError()}");

            // Create the window handle. Ensure it doesn't fail.
            //   - Hide the window until the Run() method is called.
            //   - This prevents an "unprepared" window from showing briefly during startup.
            _window = SDL_CreateWindow(
                options.Title,
                options.Width,
                options.Height,
                SDL_WindowFlags.SDL_WINDOW_HIDDEN);

            if (_window is null)
                throw new ApplicationException($"Failed to create the window. {SDL_GetError()}");
        }

        /// <summary>
        ///   Run the update loop and process events.
        /// </summary>

        public void Run()
        {
            OnStart?.Invoke();
            SDL_ShowWindow(_window);

            while (Running)
            {
                PollEvents();
                OnUpdate?.Invoke(0);
            }

            OnQuit?.Invoke();
            SDL_DestroyWindow(_window);
            SDL_Quit();
        }

        /// <summary>
        ///   Stop the update loop as soon as possible.
        /// </summary>

        public void Stop()
        {
            Running = false;
        }

        /// <summary>
        ///   Poll each pending event from the queue and execute the appropriate delegate.
        /// </summary>

        private void PollEvents()
        {
            SDL_Event e;

            // Poll each pending event from the queue.
            while (SDL_PollEvent(&e))

                // Figure out which delegate to execute based on the event type.
                switch ((SDL_EventType)e.type)
                {
                    case SDL_EventType.SDL_EVENT_QUIT:
                        OnQuit?.Invoke();
                        Stop();
                        break;

                    case SDL_EventType.SDL_EVENT_KEY_DOWN:
                        OnKeyDown?.Invoke((KeyCode)e.key.key);
                        break;
                }
        }
    }
}