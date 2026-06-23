# LUmaKE Overview
-------------------------------------------------------------------------------
The LUmaKE project provides 3D graphics, windowing, and audio features.

# Build Notes
-------------------------------------------------------------------------------
If you're unable to run the project due to ShaderCross dependency issues, run
this command in your linux terminal:

```bash
export LD_LIBRARY_PATH=$LD_LIBRARY_PATH:/home/lu32/Code/CoreluXP_Project/Demo/bin/Debug/net10.0/runtimes/linux-x64/native
```
Also, the file `runtimes/linux-x64/native/libspirv-cross-c-shared.so` needs renamed to `libspirv-cross-c-shared.so.0`