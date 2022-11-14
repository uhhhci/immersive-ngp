# Immersive Neural Graphics Primitives

In this project, we present immersive NGP, the first open-source VR NERF Unity package that brings high resolution, low-latency, 6-DOF NERF rendering to VR. This work is based on Nvidia's ground breaking [instant-ngp](https://github.com/NVlabs/instant-ngp) technique. Current version uses [this commit](https://github.com/NVlabs/instant-ngp/commit/54aba7cfbeaf6a60f29469a9938485bebeba24c3) of instant-ngp.

## Features

* Stereoscopic, 6-DOF, real-time, low-latency NERF VR rendering in Unity. 
        <img src=".\images\stereo-nerf-demo.gif"
        alt="plugin-files.PNG"
        style="float: center; margin-right: 10px; height:300px;" />

* DLSS Support for rendering at higher framerate.

* 6-DOF continuous locomotion in VR.

* Offline volume image slices rendering via [Unity Volume Rendering Toolkit](https://github.com/mlavik1/UnityVolumeRendering).

* Integration with [MRTK 2.8](https://github.com/microsoft/MixedRealityToolkit-Unity) for building mixed reality applications with NERF. 

    * Example: Merging a NERF volume image slices with a CAD model

        <img src=".\images\volume_operation.gif"
        alt="plugin-files.PNG"
        style="float: center; margin-right: 10px; height:200px;" />

## Dependencies

* Unity 2019.4.29 ( Use the legacy XR manager for compatibility with OpenVR)
* [instant-ngp](https://github.com/NVlabs/instant-ngp)
* Unity OpenVR desktop plugin && SteamVR
* Microsoft Mixed Reality Toolkit MRTK 2.8 (optional)
* OpenGL Graphics API
* Current version of the repository was tested on Windows 10, Windows 11, using a Oculus Quest 2. 

## Installation

1. Clone this repository: ```git clone --recursive https://github.com/uhhhci/immersive-ngp```

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
    style="float: center; margin-right: 10px; height:200px;" />

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
6. For a quick VR test of your own NERF scene, go to the ```Assets\NERF_NativeRendering\Scenes\XRTest``` scene.
7. Copy the path to your nerf model, images folder, and transform.json file to the ``` Stereo Nerf Renderer``` in the ```Nerf path``` parameters, as ilustrated below.
   
    <img src=".\images\stereo-nerf-gameobj.PNG"
    alt=".\images\stereo-nerf-gameobj.PNG"
    style="float: center; margin-right: 10px; height:300px;" />

    (Note: it is recommend to generate the model using [this instant-ngp commit](https://github.com/NVlabs/instant-ngp/commit/54aba7cfbeaf6a60f29469a9938485bebeba24c3), or just use the instant-ngp instance included in this repo).

6. Adjust DLSS settings, and image resolution as you like. 
7. Now you can run the scene in Editor :)
8. Use the joystick of the VR controllers for locomotion. 
9. Disclaimer: There is currently an issue with running the scene in Unity Editor with native plugin clean up, you might need to restart the editor when running a new scene. 

## Common Questions & Troubleshoot

1. **How to reach good framerate and lower latency**

    Beside having a good GPU, it is highly recommended to turn on DLSS support in Unity, also when building the native plugin. 

    [The instant-ngp commit](https://github.com/NVlabs/instant-ngp/commit/54aba7cfbeaf6a60f29469a9938485bebeba24c3) we use also allows saving aabb cropping in the pre-trained model snapshot. If you adjust the aabb cropping when training the model, it will saved and be loaded in Unity as well. Reducing aabb cropping could reduce the render volume, thus save some computational power. 

2. **Locomotion doesn't work**

    Make sure that SteamVR detects both of your controllers before starting the scenes in the Editors. 

## Roadmap

* Fix Editor restart issue
* Time-warp algorithm for latency compensation
* Dynamics Resolution
* Foveated NERF
* Support for OpenXR
* Support for higher Unity Version
* Real-time SLAM capture for dynamic grow dataset

## Thanks

Many thanks to the authors of these open-source repositories:

1. [instant-ngp](https://github.com/NVlabs/instant-ngp)
2. [Unity Volume Rendering](https://github.com/mlavik1/UnityVolumeRendering)
3. [Mixed Reality Toolkit](https://github.com/microsoft/MixedRealityToolkit-Unity)
4. [Unity Native Plugin Reloader](https://github.com/forrestthewoods/fts_unity_native_plugin_reloader)

## Authors

**\*Ke Li<sup>1, 2</sup>, \* Tim Rolff<sup>1,3</sup>**, Susanne Schmidt <sup>1</sup>,  Reinhard Bacher <sup>2</sup> ,  Simone Frintrop <sup>3</sup> , Wim Leemans <sup>3</sup> , Frank Steinicke <sup>1</sup> 


**\*These authors contributed equally to the work.** 

<sup>1</sup>  Human-Computer Interaction Group, Department of Informatics, Universität Hamburg

<sup>2</sup>  Deutsches Elektronen-Synchrotron DESY, Germany

<sup>3</sup> Computer Vision Group, Department of Informatics, Universität Hamburg

Contact: ke.li1@desy.de, tim.rolff@uni-hamburg.de

## Citations

```bibtex
@misc{Immersive-NGP,
  author = {Tim Rolff and Ke Li and Susanne Schmidt and Reinhard Bacher and   Simone Frintrop and Wim Leemans and Frank Steinicke},
  title = {Immersive Neural Graphics Primitives},
  year = {2022},
  publisher = {GitHub},
  journal = {GitHub repository},
  howpublished = {\url{https://github.com/uhhhci/Immersive-Neural-Graphics-Primitives}},
}
```

## Acknowledgment

This work was supported by DASHH (Data Science in Hamburg - HELMHOLTZ Graduate School for the Structure of Matter) with the Grant-No. HIDSS-0002, and the German Federal Ministry of Education and Research (BMBF).

## License

Please check [here](LICENSE.txt) to view a copy of Nvidia's license for instant-ngp and for this repository.


