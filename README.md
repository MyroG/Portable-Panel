# Portable VRChat Panel
A script for a portable panel that can be used in any VRC world.
- In VR, the panel can be opened by using the "Triggers" or the "Grab" gesture with both hands.
- On PC, just keep the "Tab" key pressed.

Requires UdonSharp.

![Showcase](https://github.com/MyroG/Portable-Panel/blob/main/Presentation.gif)

Installation is pretty easy, just attach the `PortablePanel` script on a GameObject, you can also try the prefabs included in the package.

## Parameters

### Panel
The Panel GameObject, the script should not be attached on the panel, preferably on a separate GameObject, or as a child GameObject, and for best results the panel should have a size of 1 unit (1 unit = 1 meter).

### VR specific settings :

#### Gesture Mode
Set this value to \"Grab\" if the panel should be opened with the grab gesture, or \"Triggers\" if you prefer trigger buttons.
Just be careful if you set it on "Triggers" : If your panel is a menu, then the panel might get accidentally grabbed or scaled when trying to interact with it!

####  Grabbable Panel
You can set this boolean to `true` if you want to make the panel grabbable with one hand.
I would recommend to set it on `false` if your panel also has a VRCPickup component attached to it.

#### Max Scale 
The panel can be scaled up as much as you like, but if you want you can set a max scale, and the panel will never exceed that scale.

#### Min Scale
If the panel goes bellow the \"MinScale\", it will automatically close.

#### Max Distance Before Closing The Panel (meters)
The panel will automatically close if the player walks away from it, the distance can be configured there.

### Desktop specific settings :

#### Panel Scale On Desktop
Scale of the panel for Desktop users.

## Events 
If you want to implement custom behaviors to the panel, for instance when the panel closes, or when it gets dropped, you can create a class that inherits from `PortablePanel` ann override the events you need to override.

| Event name      | Parameters                                        | Behavior                                                                                                                               | Return |
|-----------------|---------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------|--------|
| OnPanelOpening  |                                               | Gets called when the panel opens.                                                                                                      | True - If the panel needs to be opened. If you want to open the panel manually, you can return `false`. |
| OnPanelClosing  |                                               | Gets called when the panel is about to get closed, so it is called when the panel is not closed yet.                                   | True - If the panel needs to be closed. If you want to close the panel manually, you can return `false` instead. |
| OnPanelGrab     |                                               | Gets called when the panel is getting grabbed, either by one hand or with both hands. If `Grabbable Panel` is set to false, only scaling triggers that event. |  |
| OnPanelDrop     |                                               | Gets called when the panel is dropped.                                                                                                  |  |
| OnPanelScaled   | float oldScale, float newScale                   | Gets called when the panel gets scaled.                                                                                                 |  |



## Public methods
A few public methods can be called from an external script :

| Function Name          | Return | Explanation                                                                                                          |
|------------------------|--------|----------------------------------------------------------------------------------------------------------------------|
| `ForceClosePanel()`    |        | Closes the panel even if it is currently being held or scaled.                                                      |
| `ForceOpenPanel()`     |        | Opens the panel:<br> - On Desktop, the panel will be shown on the screen and can be closed again with the "Tab" key.<br> - In VR, the panel will be placed in front of the player's face. |                                                   |
| `IsPanelHoldByOneHand()`| bool  | Returns true if the panel is being held with one hand.                                                              |

## License
MIT, see the include LICENSE file

## Credits
No need to credit me, but if you want you can credit my VRChat username (MyroP) with or without a link to this GitHub page.

## Socials and contact
For bug reports or suggestion, please use the "Issue" tab, but you can also contact me on X/Twitter (my DM's are open)
- My VRChat profile : https://vrchat.com/home/user/usr_0d0d4ccf-7352-46bd-b1d1-ec804f0c3490
- My VRCList profile : https://vrclist.com/user/MyroP
- My Twitter/X account : https://x.com/MyroDev or https://twitter.com/MyroDev
- Tip jar: https://www.patreon.com/myrop
