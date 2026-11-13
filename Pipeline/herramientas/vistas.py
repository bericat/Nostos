import bpy, sys, math, json, mathutils
# uso: vistas.py -- base modelo prefijo_salida vistas.json [muestras]
a=sys.argv[sys.argv.index("--")+1:]
A,modelo,pref,vistas=a[0],a[1],a[2],json.load(open(a[3])); muestras=int(a[4]) if len(a)>4 else 20
bpy.ops.wm.read_factory_settings(use_empty=True)
mat=bpy.data.materials.new("P"); mat.use_nodes=True
nt=mat.node_tree; b=nt.nodes["Principled BSDF"]; t=nt.nodes.new("ShaderNodeTexImage"); t.image=bpy.data.images.load(A+"/Kit/Paleta.png"); t.interpolation='Closest'
nt.links.new(t.outputs[0], b.inputs["Base Color"]); b.inputs["Roughness"].default_value=0.9
bpy.ops.import_scene.fbx(filepath=A+"/"+modelo+".fbx")
objs=[o for o in bpy.data.objects if o.type=='MESH']
for o in objs: o.data.materials.clear(); o.data.materials.append(mat)
bpy.ops.mesh.primitive_plane_add(size=600, location=(0,0,0))
pl=bpy.context.object; mp=bpy.data.materials.new("F"); mp.use_nodes=True
mp.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value=(0.30,0.36,0.12,1); pl.data.materials.append(mp)
cam=bpy.data.objects.new("c",bpy.data.cameras.new("c")); bpy.context.scene.collection.objects.link(cam)
sun=bpy.data.objects.new("s",bpy.data.lights.new("s",'SUN')); sun.data.energy=4.0; sun.data.color=(1,.92,.8); sun.data.angle=math.radians(2)
bpy.context.scene.collection.objects.link(sun)
luz=bpy.data.objects.new("l",bpy.data.lights.new("l",'POINT')); luz.data.energy=120; luz.location=(0,0,2.4); luz.data.shadow_soft_size=0.3
bpy.context.scene.collection.objects.link(luz)
w=bpy.data.worlds.new("w"); bpy.context.scene.world=w; w.use_nodes=True
bg=w.node_tree.nodes["Background"]; bg.inputs[0].default_value=(0.5,0.6,0.9,1); bg.inputs[1].default_value=1.0
s=bpy.context.scene; s.camera=cam; s.render.engine='CYCLES'; s.cycles.samples=muestras; s.cycles.device='CPU'; s.cycles.use_denoising=True
s.view_settings.view_transform='Standard'
for v in vistas:
    cam.location=v["cam"]; d=mathutils.Vector(v["mira"])-cam.location
    cam.rotation_euler=d.to_track_quat('-Z','Y').to_euler(); cam.data.lens=v.get("lente",28); cam.data.clip_start=0.05
    az=math.radians(v.get("sol",135)); el=math.radians(48)
    dirv=mathutils.Vector((math.cos(az)*math.cos(el), math.sin(az)*math.cos(el), math.sin(el)))  # hacia el sol
    sun.rotation_euler=(-dirv).to_track_quat('-Z','Y').to_euler()
    for o in objs: o.hide_render = any(o.name.startswith(h) for h in v.get("oculta",[]))
    luz.hide_render = not v.get("luz", False)
    luz.location = v.get("luz_pos", (0, 0, 2.4))
    s.render.resolution_x=v.get("w",1000); s.render.resolution_y=v.get("h",700); s.render.filepath=pref+v["n"]+".png"
    bpy.ops.render.render(write_still=True)
