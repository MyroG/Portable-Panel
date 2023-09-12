# Portable VRChat Panel
A script for a customizable portable panel that can be used in any VRC world. You can use it to create menus, portable video players etc.
- In VR, put your hands close together, then use the "Triggers" or the "Grab" gesture to open the menu.
- On PC, just keep the "Tab" key pressed.

Requires UdonSharp.

It can be tested in my Prefab world called "Assets I released" : https://vrchat.com/home/world/wrld_22e9b1a3-1d2e-4800-b46d-ce3501b07001

## Prefabs

This package includes two examples :

A basic panel 

![Showcase](https://github.com/MyroG/Portable-Panel/blob/main/Res/Presentation1.gif)

A more complex example where the panel can be thrown away. Once the player is too far away from the panel, it dissintegrates, which is done with a basic particle animation.

![Showcase](https://github.com/MyroG/Portable-Panel/blob/main/Res/Presentation2.gif)


 
## Installation

Version 1.2 requires VRCSDK 3.2.2 or above.
Older versions work with older VRCSDKs, but they do not include features related to avatar scaling.

Installation is pretty easy, just attach the `PortablePanel` script on a GameObject, you can also try the prefabs included in the package. Do not attach that script directly on the panel, attach it rather on a separate GameObject.
Once you added the `PortablePanel` component, you'll notice a few settings, I'll explain them bellow:

![Parameters](https://github.com/MyroG/Portable-Panel/blob/main/Res/Parameters.png)

Certain settings like `Max Scale`, `Min Scale`, `Max Distance Before Closing The Panel`, and `Panel Scale On Desktop` are all based on real-world measurements, like meters you'd use in your room, rather than Unity's internal units. These values automatically adjust based on the size of your avatar. So, for example, if you set `Max Distance Before Closing The Panel` to 5 meters, and your avatar is 1m80 tall, the panel will close when it's 5 meters away from your avatar. But if your avatar is smaller, say 90cm, the panel will close at 2m50, which is half the distance. From your point of view as a player, it will still feel like the panel is closing at 5 meters, even though it adapts to your avatar's size

| Parameter                            | Explanation                                                                                                          |
|--------------------------------------|----------------------------------------------------------------------------------------------------------------------|
| Panel                                | The Panel GameObject. As mentioned above, the script should not be attached to the panel directly. It is preferable to attach it on a separate GameObject or as a child GameObject. For best results, the panel should have a size of 1 unit (1 unit = 1 meter). If you need to resize the panel, do not scale the panel directly, resize the child GameObject instead, as the initial scale of the panel will be overriden by the script |
| Tab On Hold                    | Desktop only setting, by default the player needs to keep the Tab button pressed, which also unlocks the mouse cursor at the same time aand allows the player to click around, this feature can cause issues if the panel has an input field (the player wouldn't be able to interact with the input field), in that case it is recommended to turn that setting off. |
| Gesture Mode                         | Set this value to "Grab" if the panel should be opened with the grab gesture, "Triggers" if you prefer trigger buttons, or "Both" if the panel should be opened with a combination of both gestures. Be careful when setting it to "Triggers" as the panel might get accidentally grabbed or scaled when trying to interact with it, especially if it's a menu. |
| Closing Behaviour                         | Set this value to "Closing" if you want to close/hide the panel, set it to "Respawning" if you want the panel to respawn at it's original location. |
| Grabbable Panel                      | Set this boolean to `true` if you want to make the panel grabbable with one hand. It is recommended to set it to `false` if your panel also has a VRCPickup component attached to it. |
| Max Scale                            | The panel can be scaled up as much as you like, but if you want you can set a max scale, and the panel will not exceed that scale. |
| Min Scale                            | The panel cannot be scaled below the "MinScale", can be useful if your panel contains stuff that cannot be scaled down so much, otherwise you can keep that value low or event set it to 0                                                 |
| Max Distance Before Closing The Panel | The panel will automatically close if the player walks away from it. The distance can be configured here.     |
| Panel Scale On Desktop               | Desktop-only setting: Scale of the panel for Desktop users.                                                            |
| Delay Initialisation                 | This is an ugly hack to prevent certain prefabs to break.<br/>
When you launch the instance, the panel gets automatically hidden from the player, and that during the loading screen. In most cases, that shouldn't cause any issues, but hiding the panel disables any script that is attached to that panel, which can break certain scripts that really need to get initialised during the loading screen. For instance "AliceD" noticed that certain video player controls attached to the panel could break because of that, a big thanks to AliceD for pointing that out.
If that happens, you can check that checkbox, this will initialise the panel shortly before the loading screen ends, and ensure that most prefabs attached to that panel will initialise properly, the drawback is that you cannot disable the `PortablePanel` script at the start of the world, as it will break the script...|

The prefab `AndroidPanelModule` adds an overlay so the Panel can easily be opened and closed on Android devices, it adds a screen space canvas with a button, the panel can be customized if needed.
 I would recommend to add it into your scene so Android users can open the panel on their device, the field `Panel Instance` at the prefab root needs to reference your panel.
 
## Events 
If you want to implement custom behaviors to the panel, for instance when the panel closes, or when it gets dropped, you can create a class that inherits from `PortablePanel` ann override the events you need to override.

| Event name      | Parameters                                        | Behavior                                                                                                                               | Return |
|-----------------|---------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------|--------|
| `OnPanelOpening`  |                                               | Gets called when the panel opens.                                                                                                      | True - If the panel needs to be opened. If you want to open the panel manually, you can return `false`. |
| `OnPanelClosing`  |                                               | Gets called when the panel is about to get closed, so it is called when the panel is not closed yet.                                   | True - If the panel needs to be closed. If you want to close the panel manually, you can return `false` instead. |
| `OnPanelGrab`     |                                               | Gets called when the panel is getting grabbed, either by one hand or with both hands. If `Grabbable Panel` is set to false, only scaling triggers that event. |  |
| `OnPanelDrop `    |                                               | Gets called when the panel is dropped.|  |
| `OnPanelScaled`   | float oldScale, float newScale                   | Gets called when the panel gets scaled.|  |
| `OnStart`   |                    | Gets called on Start. Use it if you need to initialize certain values                                                                                                |  |



## Public methods
A few public methods can be called from an external script :

| Function Name          | Return | Explanation                                                                                                          |
|------------------------|--------|----------------------------------------------------------------------------------------------------------------------|
| `ForceClosePanel()`    |        | Closes the panel even if it is currently being held or scaled.                                                      |
| `ForceOpenPanel()`     |        | Opens the panel:<br> - On Desktop, the panel will be shown on the screen and can be closed again with the "Tab" key.<br> - In VR, the panel will be placed in front of the player's face. |                                                   |
| `IsPanelHoldByOneHand()`| bool  | Returns true if the panel is being held with one hand.                                                              |
| `SetRespawnPoint(Vector3 position, Quaternion rotation, Vector3 scale)`     |        | Sets the respawn point of the panel, which can be useful if you want to move the panel to a different place. |                                                   |
| `RespawnPanel`     |        | Respawns the panel, only works  when `Closing Behaviour` is set to `Respawning`, it has a similar behaviour as `ForceClosePanel`, except that `ForceClosePanel` checks if the panel already got closed. |                                                   |

## Constants
I use two contants I didn't exposed in the inspector, because I didn't wanted to fill up the inspector with parameters no one will change, but if needed you can change them.
- MAX_DISTANCE_HAND_GESTURE : Max distance between both hands to trigger a panel opening, it is set at 30cm, so to open the panel the distance between your hands should not exceed 30 centimeters.
- TIME_INTERVAL_HAND_GESTURE : To open the panel and to scale it, the right and left hand gestures should occur with a time gap of less than the time given by that constant, it is set to 0.15 second.

## License
MIT, see the include LICENSE file

## Credits
No need to credit me, but if you want you can credit my VRChat username (MyroP) with or without a link to this GitHub page.

## Socials and contact
For bug reports or suggestion, please use the "Issue" tab, but you can also contact me on X/Twitter (my DM's are open)
- My Twitter/X account : https://x.com/MyroDev or https://twitter.com/MyroDev
- My VRChat profile : https://vrchat.com/home/user/usr_0d0d4ccf-7352-46bd-b1d1-ec804f0c3490
- My VRCList profile : https://vrclist.com/user/MyroP
- Tip jar: https://www.patreon.com/myrop

