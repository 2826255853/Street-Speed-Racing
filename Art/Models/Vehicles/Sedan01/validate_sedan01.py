"""One targeted asset validation pass; no project gameplay tests are needed."""
import bpy, json, math
from pathlib import Path
from mathutils import Vector

SOURCE=Path(__file__).resolve().parent
PROJECT=SOURCE.parents[3]
MODELS=PROJECT/'Assets/Art/Vehicles/Sedan01/Models'
result={'blender':bpy.app.version_string,'levels':[]}
bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'Sedan01.blend'))
collection=bpy.data.collections['SEDAN01_SOURCE']
source_meshes=[o for o in collection.objects if o.type=='MESH']
assert source_meshes and all(len(o.data.polygons)>0 for o in source_meshes), 'Empty source mesh'
for name in ['Wheel_FL','Wheel_FR','Wheel_RL','Wheel_RR']:
    pivot=bpy.data.objects[name+'__SOURCE']
    assert abs(pivot.location.z-.325)<1e-5
    assert len(pivot.children)>20
for material in [m for m in bpy.data.materials if m.name.startswith('Sedan01_')]:
    outputs=[n for n in material.node_tree.nodes if n.type=='OUTPUT_MATERIAL' and n.is_active_output]
    assert outputs and outputs[0].inputs['Surface'].is_linked, material.name
result['editableSourceMeshes']=len(source_meshes)
last=10**9
for level in range(3):
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(MODELS/f'Sedan01_LOD{level}.fbx'))
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
    assert len(objects)==6, [o.name for o in objects]
    assert {o.name for o in objects}=={'Body','Glass','Wheel_FL','Wheel_FR','Wheel_RL','Wheel_RR'}
    points=[];triangles=0;degenerate=0
    for ob in objects:
        me=ob.data
        assert me.uv_layers and len(me.polygons)>0,ob.name
        assert all(m is not None for m in me.materials),ob.name
        assert ob.matrix_world.determinant()>0,ob.name
        for v in me.vertices:
            p=ob.matrix_world@v.co
            assert all(math.isfinite(c) for c in p)
            points.append(p)
        me.calc_loop_triangles();triangles+=len(me.loop_triangles)
        degenerate+=sum(t.area<1e-12 for t in me.loop_triangles)
    assert triangles<last,'Nondecreasing LOD triangle budget'
    last=triangles
    low=Vector([min(p[i] for p in points) for i in range(3)])
    high=Vector([max(p[i] for p in points) for i in range(3)])
    size=high-low
    assert 4.3<size.y<4.9 and 1.9<size.x<2.3 and 1.3<size.z<1.6,tuple(size)
    assert abs(low.z)<.025,tuple(low)
    pivots={o.name:tuple(round(c,5) for c in o.matrix_world.translation) for o in objects if o.name.startswith('Wheel_')}
    assert all(abs(p[2]-.325)<.001 for p in pivots.values()),pivots
    result['levels'].append({'level':level,'triangles':triangles,'sizeBlenderXYZ':list(size),'wheelPivots':pivots,'degenerateTriangles':degenerate})
result['passed']=True
(SOURCE/'blender_validation_report.json').write_text(json.dumps(result,indent=2),encoding='utf8')
print('SEDAN01_GEOMETRY_VALIDATED',json.dumps(result),flush=True)
