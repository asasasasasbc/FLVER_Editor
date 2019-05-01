# FLVER_Editor
A rough editor to edit Fromsoftware game's FLVER file (Sekiro, Dark Souls, Bloodborne etc.)

Author: Forsakensilver (遗忘的银灵)

1.42 Update:
-Fixed automatic back up bug.
-Added experimental bone weight importing functionality (import fbx bone)
-Added switch YZ axis functionality for convenience. (Because some format's imported model's YZ axis need to be switched to look correctly)

1.4 Update:
-Added improt external 3d model file functionality
	Click [ImportModel] to import external 3d model file, such as fbx, dae, obj.
	Although Mesh, normal, tangents and UV coordinates can be preserved, you
	still need to manually change texture path at the Material window.
	(Special thanks to Katalash. His DSTOOLS code helped me develop this functionality. )

-Added automatic back up functionality.
	Save a .bak file in case something goes wrong.

-Mouse Right click: check the clicked vertex information.

-Added basic mode: 
	When there are too many bones, the editor may be slow, so I added this basic mode when handling models with a lot of bones.
	You can turn this mode on when editor is loading FLVER file that has a lot of bones

1.35 Update:
-Mesh->DS3 fix: Fix DS3 model not showing in Sekiro problem.
-Mesh->Attach: Attach another flver (I wish it is working ...)
-BufferLayout: Check but not edit the buffer layout.


Basic tutorial:

1.Double click the MySFformat to start the program, choose the flver file you want to edit.
(Alternate way: drag the flver to the .exe file to auotmatic open the file, or you can set the flver file open method to this .exe file.)

2.You will see two windows. One is FLVER viewer and another is FLVER Bones.

[FLVER viewer]
A window to help you check the model and see the changes you made.
Basic operation:
	-Mouse press and move: rotate your camera
	-Mouse scroll: move forward/backward
	-Numpad 2 and 8: Move up/down your camera
	-F1: Render mode: line
	-F2: Render mode: mesh
	-F3: Render mode: both
	-F: Refresh the scene
	-Mouse Right click: check the clicked vertex information.
	
[FLVER Bones]
This is your main working bench. In this window you can edit some basic bone and header information.

Components in the left pannel: 
	-A list of bones and their basic information.
		Allowing you to edit bone names and their parent/child bone information.
		Need to click the  "Modify" button in the right panel to save your change.
	-Bones Json text.
		Allowing you to directly editing all bones information in Json.
		Need to click the "ModifyJson" button in the right panel to save your Json change.
	-Header Json text.
		Allowing you to directly editing all header information in Json.
		Need to click the "ModifyJson" button in the right panel to save your Json change.
		
Components in the right panel:
	-Modify
		Save your change at the bone list part.
	-Material
		Open the [material] window.
	-Mesh
		Open the [mesh] window. So that you can transform meshes of the flver model.
	-Swap
		[Obsolete]Allowing you to swap mesh/material/bone information between two different flver file.
		It is used in some special or tricky cases and not very necessary. 
		But this is this program's first fuctionality, thus I kept it.
	-Dummy
		Open the [dummy] window. If you want to fix the sword trail and weapon art issue, click [dummy]->[SekiroFix]
		This program will automatically fix the issue.(You still need to manually change the dummy points position though.)
	-ModifyJson
		Save your change at the Json parts.
	-LoadJson
		Load an external bone Json file to change bone information.
	-BB_BoneFix
		Includes some special functionalities to help you fix some bone issues that occured in Sekiro when importing 
		Bloodborne flver files.
	-BufferLayout
		Check your current FLVER file's buffer layout information. It contains how will the program write things to the flver file.
	-Import Model
		Import external model files into the flver.
		After import, you need to click [Modify] to save your result.
		You may also need to rotate the mesh to fix the axis problem.
		
