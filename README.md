# Magic NeRF Lens

Magic NeRF lens enables various interactive VR NeRF experiences utilzing magic-lens-style visualization. This includes, 3D NeRF drawing, FoV adjustment, basic 3D manipulation (scaling, rotating, and translating), model editing and saving, etc. The repository will be continuously updated with example scenes and instructions. 

----------------------

* Please note that this is a research work, and the system is experimental and not bug-free and production ready! Please feel free to submit a pull request for improving the repositories!

* Please note that this branch enables experimentation of interacting with NeRF in VR, which can be more computationally demanding and requires high framerate to ensure a smooth VR experience. Therefore, please ensure that you are running example scenes on a high-end graphic card! (e.g. Nvidia RTX 3090) 

* Checkout our [Magic NeRF Lens paper]( https://doi.org/10.3389/frvir.2024.1377245 ) to see how we merge a NeRF with a CAD model in VR for viewing large-scale scene with one-to-one real-world scale.

    <img src=".\images\basic-magic-nerf-lens.gif"
    alt="plugin-files.PNG"
    style="float: center; margin-right: 10px; height:250px;" />


## Features

* NeRF model manipulation, crop-box adjustment, FoV adjustment.
    <p float="left">
    <img src=".\images\manipulate.gif"
    alt="plugin-files.PNG"
    style="margin-right: 10px; height:200px;" />
    <img src=".\images\crop_box.gif"
    alt="plugin-files.PNG"
    style="margin-right: 10px; height:200px;" />
    <img src=".\images\FoV.gif"
    alt="plugin-files.PNG"
    style="margin-right: 10px; height:200px;" />
    </p>

* NeRF model editing and model saving.
    <p float="left">
    <img src=".\images\editing.gif"
    alt="plugin-files.PNG"
    style="margin-right: 10px; height:200px;" />
    <img src=".\images\3DNeRFDrawing.gif"
    alt="plugin-files.PNG"
    style="margin-right: 10px; height:200px;" />
    </p>

* Depth textures in Unity and depth occlusion effects; AR NeRF, etc (WIP) 
    <p float="left">
    <img src=".\images\occlusion.gif"
    alt="plugin-files.PNG"
    style="margin-right: 10px; height:200px;" />
    <img src=".\images\AR-NeRF.gif"
    alt="plugin-files.PNG"
    style="margin-right: 10px; height:200px;" />
    </p>

## Dependencies

* Unity 2019.4.29 ( Use the legacy XR manager for compatibility with OpenVR)
* [instant-ngp](https://github.com/NVlabs/instant-ngp)
* Unity OpenVR desktop plugin && SteamVR
* Microsoft Mixed Reality Toolkit MRTK 2.8 (already included in the Unity project)
* OpenGL Graphics API
* Current version of the repository was tested on Windows 10, Windows 11, using a Oculus Quest 2. 

## Installation

1. Clone this repository: ```git clone --recursive https://github.com/uhhhci/immersive-ngp```

2. Checkout the ``magic-nerf-lens`` branch: 
    
    ``git pull magic-nerf-lens && git checkout magic-nerf-lens``

2. Make sure you have all the dependencies for [instant-ngp](https://github.com/NVlabs/instant-ngp) installed before proceed.

3. Update dependencies for submodules

    ```
    git submodule sync --recursive
    git submodule update --init --recursive
    ```

4. Build the instant-ngp project, similar to the build process for the original instant-ngp project.

    ```
    cmake . -B build
    cmake --build build --config RelWithDebInfo -j
    ```

5. After succesful build, copy the following plugin files from ```\instant-ngp\build\``` folder to the ```\stereo-nerf-unity\Assets\Plugins\x86_64``` folder.

    <img src=".\images\plugin-files.PNG"
    alt="plugin-files.PNG"
    style="float: center; margin-right: 10px; height:150px;" />

5. Now instant-ngp can be loaded as native plugins via Unity.

## Usage for Immersive NERF Rendering

1. For Oculus Quest 2, Lunch Oculus Rift, and connect the headset to the PC via Link Cable, or Air Link. 
2. Launch SteamVR, make sure that SteamVR detects your headset and controllers. 
3. For a quick demo train a model using the fox scene via:
   ```
   build\testbed.exe --scene ..\data\nerf\fox
   ```
   and safe a snapshot of the instant-ngp model through Instant-ngp > Snapshot > Save 
5. Open the stereo-nerf-unity Unity project with Unity 2019.4.29. 
6. For a quick VR test of your own NERF scene, go to the ```Assets\NERF_NativeRendering\Scenes\01_XRTest``` scene.
7. Copy the path to your nerf model, images folder, and transform.json file to the ```Exo Stereo NeRF Renderer``` in the ```Nerf_path``` parameters, as ilustrated below.
   
    <img src=".\images\stereo-nerf-gameobj.PNG"
    alt=".\images\stereo-nerf-gameobj.PNG"
    style="float: center; margin-right: 10px; height:300px;" />

    (Note: please generate the nerf model using [this instant-ngp commit](https://github.com/NVlabs/instant-ngp/commit/54aba7cfbeaf6a60f29469a9938485bebeba24c3) and above, or just use the instant-ngp instance included in this repo).

6. Adjust DLSS settings, and image resolution as you like. 
7. Now you can run the scene in Editor :)
8. Use the joystick of the VR controllers for locomotion. 
9. Use controllers to adjust the transform of the NeRF object using MRTK's object manipulator. 


## Citations

```bibtex
@ARTICLE{magic-nerf-lens,
        AUTHOR={Li, Ke  and Schmidt, Susanne  and Rolff, Tim  and Bacher, Reinhard  and Leemans, Wim  and Steinicke, Frank },
        TITLE={Magic NeRF lens: interactive fusion of neural radiance fields for virtual facility inspection},
        JOURNAL={Frontiers in Virtual Reality},
        VOLUME={5},
        YEAR={2024},
        URL={https://www.frontiersin.org/journals/virtual-reality/articles/10.3389/frvir.2024.1377245},
        DOI={10.3389/frvir.2024.1377245},
        ISSN={2673-4192}}
```

Contact: ke.li1@desy.de

## Acknowledgment

This work was supported by DASHH (Data Science in Hamburg - HELMHOLTZ Graduate School for the Structure of Matter) with the Grant-No. HIDSS-0002, and the German Federal Ministry of Education and Research (BMBF).

## License

Please check [here](LICENSE.txt) to view a copy of Nvidia's license for instant-ngp and for this repository.


