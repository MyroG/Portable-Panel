# Portable VRChat Panel
A script for a portable panel that can be used in any VRC world.
- In VR, the panel can be opened by using the "Triggers" or the "Grab" gesture with both hands.
- On PC, just keep the "Tab" key pressed.

Installation is pretty easy, just attach that script on a GameObject, you can also try the prefab included in the package.

## Parameters

### Panel
The Panel GameObject, the script should not be attached on the panel, preferably on a separate GameObject, and for best results the panel should have a size of 1 Unity unit (1 unit = 1 meter)

### VR specific settings

#### Gesture Mode
Set this value to \"Grab\" if the panel should be opened with the grab gesture, or \"Triggers\" if you prefer trigger buttons.
Just be careful if you set it on "Triggers" : If your panel is a menu, then the panel might get acidentally grabbed or scaled whern trying to interact with it!

####  Grabbable Panel
You can set this boolean to \"True\" if you want to make the panel grabbable with one hand

#### Max Scale
The panel can be scaled up as much as you like, but if you want you can set a max scale, and the panel will never exceed that scale

#### Min Scale
If the panel goes bellow the \"MinScale\", it will automatically be closed

#### Max Distance Before Closing The Panel
If the player walks away from the panel, you can automatically close it by setting a value bellow

### Desktop specific settings

#### Panel Scale On Desktop
Scale of the panel for Desktop users

## Events 
There are two events that can be overriden : `OnPanelOpen` and `OnPanelOpen`, so if you want to execute special actions when the menu opens or closes, you can create a class that inherits from `PortablePanel` and override those events.

## License
MIT

## Credits
No need to credit me, but if you want you can credit my VRChat username (MyroP) with or without a link to this GitHub page.
		