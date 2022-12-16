# Bezier Painter

The Bezier Painter is a Unity Editor tool that makes use of bezier splines in order to paint paths onto Unity Terrain. 
It can both allow the user to paint textures onto the terrain, as well as adjust the terrain's heightmap, all by using 
Bezier Splines.

### Installation

Download and extract Zip file. Open project using UnityHub.

### How to use

There already exists a sample scene with terrain and painter objects.

Under prefabs, there is a Bezier Painter prefab with variables already set that you can drag into a scene to start painting.

To paint, simply check whether you wish to paint textures or height (or both), then click start painting. While painting, you
can adjust the Bezier nodes to real-time adjust the terrain. Once you are happy with the terrain, click 'Bake'. Alternatively,
to revert the terrain to before you started painting, click 'Revert Changes'.
