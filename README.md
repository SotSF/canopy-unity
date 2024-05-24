### Overview
Node-editor based visualization and texture synthesis software primarily for [The Canopy](https://se.cretfi.re/canopy/)

### Getting started
1. [Get Unity Hub](https://unity.com/download) for your platform. It's a launcher that manages Unity Editor versions & Unity projects. ![image](https://github.com/SotSF/canopy-unity/assets/284311/882488c7-c855-4bfa-ac7e-2da6383aa3b3)
2. Clone this repo
3. Once Unity Hub is finished downloading and installed, run it, open the `Projects` tab, and add this repo. ![image](https://github.com/SotSF/canopy-unity/assets/284311/6997b6b2-98ec-4838-b12e-252bb8e31adf)
4. Install the Unity Editor version associated with the project through the Hub interface (currently `6000.0.2f1`)
5. Launch the canopy-unity project (the first launch will take some time as the build cache populates)
6. Once the Editor is open, you can run the project by clicking the triangular `Play` button in the top center ![image](https://github.com/SotSF/canopy-unity/assets/284311/7a56cf59-f35b-49f0-a66a-db804f9ad159)
7. The main `Game` window should now become a blank canvas with a menu bar across its top ![image](https://github.com/SotSF/canopy-unity/assets/284311/e75becaa-9e49-402b-8015-afbe1113dc72)
8. You can click `File` => `Load canvas` to load an existing node graph. `canopy-unity/Assets/TextureSynthesis/Resources/CanvasSaves/MIDIMixAssignedFluidMinis.asset` is a good starting point with a setup to run the fluid sim. You may need to do `File` => `Save Canvas` to fix some broken textures on startup.

### Developing
- Recommend vs code for developing on Mac, or Visual Studio for Windows. This can be set in the Unity Editor preferences under `Edit` => `Preferences` => `External Tools`:
  ![image](https://github.com/SotSF/canopy-unity/assets/284311/5674cb2f-e709-4660-b16b-3184c2e36e88)
  ![image](https://github.com/SotSF/canopy-unity/assets/284311/1248b2f1-8d1d-4491-8836-fdcddd85a762)
- Nodes are defined in C# and use HLSL compute shaders to run their texture manipulations. See [HSVNode.cs](https://github.com/SotSF/canopy-unity/blob/main/Assets/Scripts/TextureSynthesis/Nodes/Filter/HSVNode.cs) as a good simple example
