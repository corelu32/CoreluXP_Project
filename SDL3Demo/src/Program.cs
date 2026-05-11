using SDL;
using static SDL.SDL3;

unsafe
{
    // INITIALIZE SDL3
    
    if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO))
        throw new Exception($"Failed to initialize SDL3. {SDL_GetError()}");

    bool running = true;
    SDL_Window* window = null;
    SDL_Renderer* renderer = null;
    SDL_GPUDevice* gpuDevice = null;

    // CREATE WINDOW
    
    window = SDL_CreateWindow(
        "SDL3 DEMO",
        800,
        600,
        // Hide the window until its ready.
        SDL_WindowFlags.SDL_WINDOW_HIDDEN); 
    
    if (window is null)
        throw new Exception($"Failed to create the window. {SDL_GetError()}");

    // CREATE RENDERER
    
    renderer = SDL_CreateRenderer(window, (byte*)null);

    if (renderer is null)
        throw new Exception($"Failed to create the renderer. {SDL_GetError()}");

    // CREATE GRAPHICS DEVICE
    
    gpuDevice = SDL_CreateGPUDevice(
        SDL_GPUShaderFormat.SDL_GPU_SHADERFORMAT_SPIRV,
        true,
        (byte*) null);

    if (gpuDevice is null)
        throw new Exception($"Failed to create the GPU device. {SDL_GetError()}");

    SDL_ShowWindow(window);
        
    // APPLICATION LOOP

    while (running)
    {
        SDL_Event sdlEvent;

        // POLL EVENTS
        
        while (SDL_PollEvent(&sdlEvent))
            switch ((SDL_EventType)sdlEvent.type)
            {
                case SDL_EventType.SDL_EVENT_QUIT:
                    running = false;
                    break;
            }

        // UPDATE
            
        SDL_SetRenderDrawColor(renderer, 0, 0, 30, 255);
        SDL_RenderClear(renderer);
        SDL_RenderPresent(renderer);
    }

    // CLEANUP
    
    SDL_DestroyGPUDevice(gpuDevice);
    SDL_DestroyRenderer(renderer);
    SDL_DestroyWindow(window);
    SDL_Quit();
}