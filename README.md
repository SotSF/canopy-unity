### Overview
Unity-based controller and visualizer for the Canopy.

### Developing
1. [Get the Unity editor](https://unity3d.com/get-unity/download) for your platform. It's free.
2. Once it's installed, click the 'Open' button and select this repository's folder.
3. Open the "Canopy" scene under the "Assets/Scenes/" directory in the 'Project' tab (analogous to the assets or static files associated with a web app) 
4. To add a new pattern, click the 'Tools' menu, then select 'NodeSystem/Create new node'.
5. This will open a wizard with some fields such as name, etc. For a pattern that generates images, choose Node type = TickingNode, and Template = Texture Generator.
6. Click 'Create', and a new Node class will be generated in `Assets/Scripts/TextureSynthesis/Nodes`, with a matching shader in `Assets/Scripts/TextureSynthesis/Resources/NodeShaders`.
7. Double click the node or shader from the `Project` tab of the Unity editor, and it will open your associated IDE to edit the file.
8. See the [video tutorial](https://www.youtube.com/watch?v=v51evuoNDsw) I made about this for information and a step-by-step walkthrough.

