"""Original Sedan01 model. Run with Blender 5.2 --background --python this_file.
Reference: Art/References/Vehicles/Sedan01_Reference.png only.
Conceptual coordinates: X along car (front negative), Y across, Z up; converted
to Blender -Y forward / Z up at construction. All dimensions are metres.
"""
import bpy, math, json, os, sys
from pathlib import Path
from mathutils import Vector
from math import sin, cos, pi, sqrt

ROOT = Path(__file__).resolve().parents[4]
SRC = Path(__file__).resolve().parent
OUT = ROOT / 'Assets/Art/Vehicles/Sedan01/Models'
RENDERS = SRC / 'Renders'
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for d in list(bpy.data.materials): bpy.data.materials.remove(d)
scene=bpy.context.scene
scene.unit_settings.system='METRIC'
scene.unit_settings.scale_length=1
scene.render.engine='CYCLES'
scene.cycles.samples=48
scene.cycles.use_denoising=True
scene.render.resolution_x=1440
scene.render.resolution_y=960
scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'
scene.render.film_transparent=False
scene.world.color=(.16,.16,.16)
scene.render.image_settings.color_mode='RGB'

def xyz(p): return (p[1],-p[0],p[2])
def mat(name,color,metal=0,rough=.4,alpha=1,coat=0,emission=0):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,alpha);m.use_nodes=True
    p=next((n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
    if p is None:
        p=m.node_tree.nodes.new('ShaderNodeBsdfPrincipled')
        output=m.node_tree.nodes.new('ShaderNodeOutputMaterial')
        m.node_tree.links.new(p.outputs['BSDF'],output.inputs['Surface'])
    p.inputs['Base Color'].default_value=(*color,alpha)
    p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
    p.inputs['Coat Weight'].default_value=coat
    if alpha<1:
        p.inputs['Alpha'].default_value=alpha
        p.inputs['Transmission Weight'].default_value=.15
        p.inputs['IOR'].default_value=1.46
        m.surface_render_method='DITHERED'
    if emission:
        p.inputs['Emission Color'].default_value=(*color,1)
        p.inputs['Emission Strength'].default_value=emission
    return m
PAINT=mat('Sedan01_SilverPaint',(.46,.49,.50),.78,.27,coat=.6)
RUBBER=mat('Sedan01_Rubber',(.014,.017,.019),0,.76)
BLACK=mat('Sedan01_Graphite',(.035,.043,.048),.55,.32)
GLASS=mat('Sedan01_Glass',(.12,.19,.21),.05,.13,.38,coat=.45)
ALLOY=mat('Sedan01_Alloy',(.29,.32,.34),.88,.25)
CHROME=mat('Sedan01_Chrome',(.65,.70,.72),.95,.16)
LENS=mat('Sedan01_ClearLens',(.64,.73,.76),.45,.16,coat=.7)
RED=mat('Sedan01_RedLens',(.42,.012,.018),.26,.22,coat=.8,emission=.12)
AMBER=mat('Sedan01_AmberLens',(.9,.25,.025),.18,.25,coat=.7)
TRIM=mat('Sedan01_Interior',(.043,.051,.055),.02,.83)
PLATE=mat('Sedan01_Plate',(.75,.79,.77),.2,.36)
MATS=[PAINT,RUBBER,BLACK,GLASS,ALLOY,CHROME,LENS,RED,AMBER,TRIM,PLATE]

model=bpy.data.collections.new('SEDAN01_SOURCE');scene.collection.children.link(model)
studio=bpy.data.collections.new('STUDIO_NOT_EXPORTED');scene.collection.children.link(studio)
body=[]; wheels={}; active_list=body

def place(obj,collection=model):
    for c in list(obj.users_collection):c.objects.unlink(obj)
    collection.objects.link(obj)
    return obj
def mesh(name,verts,faces,material,bevel=0,smooth=True):
    me=bpy.data.meshes.new(name);me.from_pydata([xyz(v) for v in verts],[],faces);me.update()
    ob=bpy.data.objects.new(name,me);model.objects.link(ob);me.materials.append(material)
    for f in me.polygons:f.use_smooth=smooth
    if bevel:
        mod=ob.modifiers.new('Small manufactured edge radius','BEVEL');mod.width=bevel;mod.segments=3
        wn=ob.modifiers.new('Planar panel normals','WEIGHTED_NORMAL');wn.keep_sharp=True;wn.weight=40
    active_list.append(ob)
    return ob
def applymods(obj):
    bpy.context.view_layer.objects.active=obj
    for mod in list(obj.modifiers):
        try:bpy.ops.object.modifier_apply(modifier=mod.name)
        except RuntimeError:pass
def cube(name,center,size,material,bevel=.01):
    x,y,z=center;a,b,c=[v/2 for v in size]
    vs=[(x+i*a,y+j*b,z+k*c) for i,j,k in [(-1,-1,-1),(-1,-1,1),(-1,1,-1),(-1,1,1),(1,-1,-1),(1,-1,1),(1,1,-1),(1,1,1)]]
    ob=mesh(name,vs,[(2,6,4,0),(5,7,3,1),(4,5,1,0),(3,7,6,2),(1,3,2,0),(6,7,5,4)],material,bevel)
    return ob
def tube(name,points,r,material,sides=8,closed=False):
    pts=[Vector(p) for p in points];vs=[];fs=[]
    for i,p in enumerate(pts):
        tangent=(pts[(i+1)%len(pts)]-pts[i-1] if closed else pts[min(i+1,len(pts)-1)]-pts[max(0,i-1)]).normalized()
        seed=Vector((0,0,1)) if abs(tangent.z)<.9 else Vector((0,1,0))
        u=tangent.cross(seed).normalized();v=tangent.cross(u).normalized()
        for j in range(sides):vs.append(p+r*(cos(j*2*pi/sides)*u+sin(j*2*pi/sides)*v))
    for i in range(len(pts) if closed else len(pts)-1):
        for j in range(sides):fs.append((i*sides+j,i*sides+(j+1)%sides,((i+1)%len(pts))*sides+(j+1)%sides,((i+1)%len(pts))*sides+j))
    if not closed:fs.extend([tuple(range(sides-1,-1,-1)),tuple((len(pts)-1)*sides+j for j in range(sides))])
    return mesh(name,vs,fs,material)
def cyl(name,a,b,r,material,n=48,r2=None):
    a=Vector(a);b=Vector(b);axis=(b-a).normalized();u=axis.cross(Vector((0,0,1)) if abs(axis.z)<.95 else Vector((0,1,0))).normalized();v=axis.cross(u)
    vs=[p+rr*(cos(t*2*pi/n)*u+sin(t*2*pi/n)*v) for p,rr in [(a,r),(b,r if r2 is None else r2)] for t in range(n)]
    fs=[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]+[tuple(range(n-1,-1,-1)),tuple(range(n,n*2))]
    return mesh(name,vs,fs,material)
def lathe_y(name,x,y,z,profile,material,n=64):
    vs=[(x+r*cos(a*2*pi/n),y+d,z+r*sin(a*2*pi/n)) for d,r in profile for a in range(n)]
    fs=[(i*n+j,i*n+(j+1)%n,(i+1)*n+(j+1)%n,(i+1)*n+j) for i in range(len(profile)-1) for j in range(n)]
    return mesh(name,vs,fs,material)
def patch(name,fn,nu,nv,material):
    vs=[fn(i/nu,j/nv) for i in range(nu+1) for j in range(nv+1)]
    fs=[(i*(nv+1)+j,(i+1)*(nv+1)+j,(i+1)*(nv+1)+j+1,i*(nv+1)+j+1) for i in range(nu) for j in range(nv)]
    return mesh(name,vs,fs,material)
def interp(x,keys):
    for i in range(len(keys)-1):
        if x<=keys[i+1][0]:
            a,b=keys[i],keys[i+1];t=max(0,min(1,(x-a[0])/(b[0]-a[0])));return a[1]*(1-t)+b[1]*t
    return keys[-1][1]
def width(x):return interp(x,[(-2.27,.71),(-2.18,.81),(-1.9,.864),(-1.4,.885),(-.9,.86),(0,.848),(.8,.865),(1.35,.89),(1.95,.862),(2.18,.82),(2.27,.75)])
def belt(x):return interp(x,[(-2.27,.70),(-2.13,.78),(-1.78,.87),(-1.03,.98),(-.76,1.005),(.8,1.005),(1.42,.99),(1.9,.945),(2.27,.89)])
def archbottom(x):
    b=.245
    for wx in [-1.365,1.335]:
        dx=abs(x-wx)
        if dx<.369:b=max(b,.325+sqrt(max(0,.369**2-dx**2)))
    return b
def sidepoint(x,z,s,offset=0):
    t=max(0,min(1,(z-.245)/(belt(x)-.245)))
    y=width(x)-.032*(1-t)+.018*sin(t*pi)
    return(x,s*(y+offset),z)

# Continuous, quad-built shoulders and side skins. Lower boundary follows the
# real wheel openings, leaving no body polygons in the wheel wells.
xs=sorted(set([-2.155+4.315*i/240 for i in range(241)]+[w+d for w in [-1.365,1.335] for d in [-.369,-.367,-.36,0,.36,.367,.369]]))
for s in [-1,1]:
    verts=[]
    for x in xs:
        lo=archbottom(x);hi=belt(x)
        for j in range(13):verts.append(sidepoint(x,lo+(hi-lo)*j/12,s))
    faces=[(i*13+j,(i+1)*13+j,(i+1)*13+j+1,i*13+j+1) for i in range(len(xs)-1) for j in range(12)]
    if s==1:faces=[tuple(reversed(f)) for f in faces]
    ob=mesh('Sculpted quarter panels '+str(s),verts,faces,PAINT)
    for wx in [-1.365,1.335]:
        pts=[]
        for i in range(65):
            a=-.20+(pi+.40)*i/64;x=wx+.369*cos(a);z=.325+.369*sin(a)
            pts.append(sidepoint(x,z,s,.004))
        tube('Rolled fender lip',pts,.009,PAINT,8)
        # Black inner arch from the painted lip to the chassis.
        patch('Wheelhouse liner',lambda u,v,w=wx,sg=s:(w+.363*cos(-.22+(pi+.44)*u),sg*(width(w)-.012-.27*v),.325+.363*sin(-.22+(pi+.44)*u)),48,5,RUBBER)

# Hood crown: narrower at the nose with pressed character lines.
def hood(u,v):
    t=2*v-1;x=-2.225+1.285*u+.077*(abs(t)**4)*((1-u)**3);w=width(x)
    z=belt(x)+.003+.045*(1-t*t)
    z+=.018*math.exp(-((abs(t)-.69)/.07)**2)*sin(pi*u)
    return(x,t*w,z)
patch('Pressed hood',hood,46,40,PAINT)
for s in [-1,1]:
    tube('Hood panel gap',[hood(t/50,0 if s<0 else 1) for t in range(51)],.0018,RUBBER,6)
patch('Cowl',lambda u,v:(-.965+.14*u,(2*v-1)*.85,1.0+.02*sin(pi*v)),6,30,BLACK)

# Roof and glazing are curved surfaces rather than tinted solid blocks.
def roof(u,v):
    x=-.42+1.18*u;t=2*v-1
    return(x,t*(.676+.012*sin(pi*u)),1.423+.033*sin(pi*u)+.018*(1-t*t))
patch('Crowned roof',roof,40,32,PAINT)
def windshield(u,v):
    t=2*v-1;x=-.978+.551*u-.032*(1-t*t)*sin(pi*u)
    return(x,t*(.804*(1-u)+.666*u),1.013+.405*u+.025*(1-t*t))
def rearwindow(u,v):
    t=2*v-1
    return(.773+.593*u+.018*(1-t*t)*sin(pi*u),t*(.674*(1-u)+.797*u),1.419-.403*u+.020*(1-t*t))
for name,fn in [('Windshield',windshield),('Heated rear glass',rearwindow)]:
    patch(name,fn,24,32,GLASS)
    for a in [0,1]:tube(name+' rubber seal',[fn(a,i/40) for i in range(41)],.012,RUBBER)
    for a in [0,1]:tube(name+' pillar',[fn(i/40,a) for i in range(41)],.022,PAINT,10)
for i in range(9):
    u=.25+i*.065;tube('Rear defroster element',[rearwindow(u,.06+j*.88/28) for j in range(29)],.00065,ALLOY,4)

# Side aperture silhouette; surface lies inward at roof height.
def sy(z):return .826-(z-1.012)*.367
def rounded_polygon(points,fraction=.12,steps=5):
    result=[]
    for i,p in enumerate(points):
        prev=points[i-1];nxt=points[(i+1)%len(points)]
        a=tuple(p[k]+(prev[k]-p[k])*fraction for k in range(2))
        b=tuple(p[k]+(nxt[k]-p[k])*fraction for k in range(2))
        for j in range(steps+1):
            t=j/steps;result.append(tuple((1-t)**2*a[k]+2*t*(1-t)*p[k]+t*t*b[k] for k in range(2)))
    return result
def sideglass(name,points,s):
    points=rounded_polygon(points,.085)
    ob=mesh(name,[(x,s*sy(z),z) for x,z in points],[tuple(range(len(points)))],GLASS)
    tube(name+' weatherstrip',[(x,s*(sy(z)+.002),z) for x,z in points],.011,RUBBER,8,True)
for s in [-1,1]:
    # Separate front and rear door windows with a narrow painted B pillar.
    front=[(-.911,1.038),(-.392,1.397),(-.27,1.421),(.075,1.42),(.075,1.041)]
    rear=[(.148,1.041),(.148,1.42),(.55,1.418),(.724,1.38),(1.17,1.054)]
    sideglass('Front door glass',front,s);sideglass('Rear door glass',rear,s)
    for path in [[(-.973,1.008),(-.414,1.435),(-.285,1.45),(.54,1.447),(.76,1.407),(1.37,1.009)], [(-.96,1.012),(1.35,1.012)]]:
        tube('Window aperture body frame',[(x,s*(sy(z)+.004),z) for x,z in path],.016,PAINT,10)
    mesh('B pillar',[(x,s*(sy(z)+.004),z) for x,z in [(.078,1.024),(.078,1.432),(.147,1.432),(.147,1.024)]],[(0,1,2,3)],BLACK)
    tube('Rear quarter glass separator',[(.76,s*sy(1.35),1.35),(.81,s*sy(1.05),1.05)],.013,BLACK)
    # Readable door shut lines, mouldings and recessed handles.
    for pts in [[(-.94,1.0),(-.99,.85),(-.96,.42),(-.90,.294),(.085,.294),(.13,.40),(.13,1.006)],[(.13,.294),(.69,.294),(.85,.35),(1.02,.61),(1.19,.81),(1.23,1.00)]]:
        sampled=[]
        for a,b in zip(pts,pts[1:]):
            steps=max(2,int(math.dist(a,b)/.012))
            sampled.extend([(a[0]+(b[0]-a[0])*i/steps,a[1]+(b[1]-a[1])*i/steps) for i in range(steps)])
        sampled.append(pts[-1])
        tube('Door shut line',[sidepoint(x,z,s,.0024) for x,z in sampled],.0022,RUBBER,6)
    tube('Door sill',[sidepoint(-.97+i*1.87/48,.25,s,.011) for i in range(49)],.026,PAINT,10)
    tube('Door protection moulding',[sidepoint(-.97+i*1.91/48,.565,s,.006) for i in range(49)],.012,PAINT,8)
    for hx in [-.055,.93]:
        cube('Handle shadow recess',sidepoint(hx,.906,s,.003),(.142,.013,.032),BLACK,.013)
        cube('Body colour pull handle',sidepoint(hx,.916,s,.023),(.125,.031,.022),PAINT,.010)
    if s==1:
        pts=[(1.55+.092*cos(i*2*pi/40),.846+.077*sin(i*2*pi/40)) for i in range(40)]
        tube('Fuel filler shut line',[sidepoint(x,z,s,.003) for x,z in pts],.0023,RUBBER,6,True)
    # Mirror plinth, streamlined housing and reflective insert.
    cube('Mirror sail',(-.78,s*.84,1.059),(.12,.035,.09),BLACK,.02)
    tube('Mirror stalk',[(-.80,s*.84,1.06),(-.79,s*.96,1.08)],.023,BLACK,10)
    cube('Painted mirror housing',(-.79,s*1.008,1.094),(.205,.122,.103),PAINT,.039)
    cube('Mirror reflective face',(-.688,s*1.009,1.096),(.004,.095,.067),CHROME,.018)
    # Small amber side repeater, matching reference placement.
    cube('Side turn signal',sidepoint(-1.02,.785,s,.006),(.066,.012,.021),AMBER,.009)

# Trunk deck, rear pillars and the broad C pillar infill.
patch('Trunk deck',lambda u,v:(1.34+.86*u,(2*v-1)*width(1.34+.86*u),1.012-.108*u+.016*(1-(2*v-1)**2)),24,32,PAINT)
for s in [-1,1]:
    mesh('C pillar infill',[(.73,s*.685,1.417),(1.375,s*.803,1.012),(1.24,s*.829,1.012),(.70,s*.70,1.366)],[(0,1,2,3)],PAINT)
    tube('Trunk lid side seam',[(1.40+i*.79/32,s*(width(1.40+i*.79/32)*.89),1.012-.108*((1.40+i*.79/32-1.34)/.86)+.008) for i in range(33)],.0025,RUBBER,6)

# Front and rear bumper shells. Cutouts are true openings, with recess walls.
def bumper(name,keys,material):
    vs=[];n=64
    for z,xmid,w in keys:
        for j in range(n+1):
            a=-pi/2+pi*j/n
            vs.append((xmid+(.115 if xmid<0 else -.10)*(1-cos(a)),w*sin(a),z))
    faces=[(i*(n+1)+j,i*(n+1)+j+1,(i+1)*(n+1)+j+1,(i+1)*(n+1)+j) for i in range(len(keys)-1) for j in range(n)]
    if keys[0][1]<0:faces=[tuple(reversed(f)) for f in faces]
    return mesh(name,vs,faces,material)
front_keys=[(.205,-2.255,.805),(.25,-2.285,.834),(.35,-2.282,.852),(.49,-2.275,.855),(.605,-2.25,.847),(.695,-2.20,.831),(.79,-2.155,.81)]
def nose(y,z):
    w=interp(z,[(k[0],k[2]) for k in front_keys]);x=interp(z,[(k[0],k[1]) for k in front_keys])
    x+=.115*(1-sqrt(max(0,1-(y/w)**2)))
    if z>.74:
        factor=min(1,(z-.74)/.058);h=hood(0,(y/w+1)/2)
        x=x*(1-factor)+h[0]*factor;z=z*(1-factor)+(h[2]-.001)*factor
    return(x,y,z)
def rounded_half(z,center,height,halfwidth,radius):
    d=abs(z-center);h=height/2
    if d>=h:return 0
    if d<=h-radius:return halfwidth
    return halfwidth-radius+sqrt(max(0,radius*radius-(d-h+radius)**2))
def nose_band(z0,z1,upper=False,solid=False):
    vs=[];fs=[];nz=max(4,round((z1-z0)/.006));ny=14
    for segment in [0,2,4,6]:
        start=len(vs)
        for i in range(nz+1):
            z=z0+(z1-z0)*i/nz;w=interp(z,[(k[0],k[2]) for k in front_keys])
            if solid:
                cuts=[-w,-w,-w,0,0,w,w,w]
            else:
                if upper:
                    mid=rounded_half(z,.687,.125,.395,.024)
                    half=rounded_half(z,.716,.164,.217,.023);center=.627
                else:
                    mid=rounded_half(z,.375,.215,.57,.045)
                    half=rounded_half(z,.382,.15,.1175,.028);center=.70
                cuts=[-w,max(-w,-center-half),-center+half,-mid,mid,center-half,min(w,center+half),w]
            a,b=cuts[segment],cuts[segment+1]
            for j in range(ny+1):vs.append(nose(a+(b-a)*j/ny,z))
        for i in range(nz):
            for j in range(ny):
                # Z then Y winding points toward the front of the car.
                q=start+i*(ny+1)+j;fs.append((q,q+ny+1,q+ny+2,q+1))
    return mesh('Front bumper shaped aperture topology',vs,fs,PAINT)
front_panels=[nose_band(.205,.49),nose_band(.49,.605,solid=True),nose_band(.605,.798,upper=True)]
rear=bumper('Rear bumper',[(.24,2.235,.798),(.30,2.285,.831),(.39,2.29,.853),(.56,2.265,.868),(.65,2.235,.855),(.735,2.215,.839),(.895,2.19,.80)],PAINT)
for bumper_ob in front_panels+[rear]:
    solid=bumper_ob.modifiers.new('Closed bumper wall','SOLIDIFY');solid.thickness=.028;solid.offset=-1
    applymods(bumper_ob)
cube('Main intake dark cavity',(-2.17,0,.375),(.035,1.15,.21),RUBBER,.035)
for s in [-1,1]:
    cube('Fog light recess',(-2.15,s*.70,.383),(.03,.232,.15),RUBBER,.029)
    for j in range(3):cube('Fog housing horizontal louver',(-2.235,s*.70,.345+j*.032),(.028,.202,.010),BLACK,.003)
    cyl('Fog lamp bezel',(-2.248,s*.735,.388),(-2.26,s*.735,.388),.036,BLACK,32)
    cyl('Fog lamp optical face',(-2.26,s*.735,.388),(-2.263,s*.735,.388),.027,LENS,32)
# Rhombus grille wires are actual geometry, restrained at normal viewing range.
for slope in [-1,1]:
    for k in range(-23,24):
        y0=k*.031;pts=[]
        for i in range(12):
            z=.29+.165*i/11;y=y0+slope*(z-.375)*.75
            if abs(y)<.535:pts.append((-2.203,y,z))
        if len(pts)>1:tube('Lower intake mesh',pts,.0019,BLACK,4)
cube('Front license mount',(-2.297,0,.557),(.023,.33,.135),BLACK,.009)
cube('Front license plate',(-2.312,0,.560),(.006,.311,.119),PLATE,.006)
for y in [-.132,.132]:cyl('Front plate fastener',(-2.316,y,.595),(-2.318,y,.595),.003,BLACK,12)

# Upper grille fitted between separate multi element lamp assemblies.
def front_x(y):return -2.25+.13*(abs(y)/.84)**2
grille=[(-.39,.735),(-.365,.636),(-.30,.618),(.30,.618),(.365,.636),(.39,.735)]
mesh('Upper grille cavity',[(front_x(y)-.006,y,z) for y,z in grille],[tuple(range(6))],RUBBER)
tube('Upper grille chrome surround',[(front_x(y)-.012,y,z) for y,z in grille],.009,CHROME,10,True)
for i in range(4):
    z=.637+i*.026;tube('Upper grille blade',[(front_x(y)-.018,y,z) for y in [-.345+j*.69/28 for j in range(29)]],.006,BLACK,6)
cube('Grille central emblem',(-2.226,0,.686),(.012,.024,.027),CHROME,.006)
for s in [-1,1]:
    outline=[(.413,.635),(.80,.649),(.824,.713),(.807,.787),(.527,.777)]
    pts=[(front_x(y)-.017,s*y,z) for y,z in outline]
    mesh('Headlamp sculpted reflector',pts,[tuple(range(len(pts)))],CHROME)
    tube('Headlamp perimeter gasket',pts,.008,RUBBER,8,True)
    # Individual reflector cups and concentric lenses.
    for yy,zz,rr in [(.565,.706,.055),(.691,.716,.058),(.465,.677,.026)]:
        x=front_x(yy)-.018
        cyl('Headlamp reflector cup',(x,s*yy,zz),(x-.012,s*yy,zz),rr,CHROME,48,r2=rr*.92)
        cyl('Projector black surround',(x-.013,s*yy,zz),(x-.017,s*yy,zz),rr*.70,BLACK,48)
        cyl('Projector optical lens',(x-.018,s*yy,zz),(x-.021,s*yy,zz),rr*.56,LENS,48)
        # Highlight ring remains geometric and visible in URP without emission.
        tube('Projector retaining ring',[(x-.022,s*yy+rr*.79*cos(j*2*pi/48),zz+rr*.79*sin(j*2*pi/48)) for j in range(48)],.0028,CHROME,6,True)
    cube('Headlamp amber indicator',(front_x(.778)-.024,s*.778,.724),(.015,.035,.074),AMBER,.009)
    # Faint outer clear cover; glasses follow angular lamp silhouette.
    lens=mesh('Headlamp outer cover',[(x-.026,y,z) for x,y,z in pts],[tuple(range(len(pts)))],GLASS)

# Splitter follows the bumper plan instead of being a rectangular block.
for s in [-1,1]:
    tube('Front splitter edge',[(-2.32+.16*(abs(y)/.86)**2,y,.205) for y in [s*i*.86/36 for i in range(37)]],.021,BLACK,10)
patch('Front chin splitter',lambda u,v:(-2.31+.15*(abs(2*v-1))**2+.14*u,(2*v-1)*.84,.205-.006*u),3,48,BLACK)

# Rear fascia, twin-section red/clear lamps, sculpted licence recess.
cube('Rear trunk face',(2.175,0,.795),(.09,1.55,.24),PAINT,.06)
cube('Rear plate recessed surround',(2.226,0,.763),(.014,.45,.185),BLACK,.018)
cube('Rear plate recess inner panel',(2.237,0,.768),(.006,.418,.160),PAINT,.014)
cube('Rear license plate',(2.245,0,.77),(.007,.31,.12),PLATE,.006)
cube('Rear trunk handle',(2.255,0,.877),(.037,.47,.027),PAINT,.012)
def rear_x(y):return 2.267-.09*(abs(y)/.86)**6
def rear_lens(name,points,s,material,depth=.013):
    pts=rounded_polygon(points,.14,5)
    offset=-.014 if material==RUBBER else .005
    n=len(pts);vs=[(rear_x(y)+offset-d,s*y,z) for d in [depth,0] for y,z in pts]
    fs=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return mesh(name,vs,fs,material,.002)
for s in [-1,1]:
    rear_lens('Rear lamp black gasket',[(.487,.747),(.487,.893),(.545,.931),(.802,.931),(.841,.897),(.831,.75)],s,RUBBER,.025)
    rear_lens('Red rear lamp upper',[(.498,.805),(.498,.887),(.549,.923),(.796,.923),(.833,.892),(.826,.808)],s,RED)
    rear_lens('Clear reverse lamp strip',[(.502,.757),(.5,.800),(.826,.800),(.819,.755)],s,LENS)
    patch('Tail lamp side wrap',lambda u,v,sg=s:(1.991+.199*u,sg*(width(1.991+.199*u)+.004),.789+.126*v+.009*sin(pi*u)),18,6,RED)
    for yy in [.593,.703]:
        tube('Tail lamp circular optical cell',[(rear_x(s*yy+.041*cos(j*2*pi/40))+.001,s*yy+.041*cos(j*2*pi/40),.858+.036*sin(j*2*pi/40)) for j in range(40)],.003,RED,6,True)
    for i in range(5):
        yy=s*(.535+i*.048)
        tube('Rear clear lens fluting',[(rear_x(yy)+.001,yy,.762),(rear_x(yy)+.001,yy,.792)],.0016,CHROME,4)
    cube('Rear bumper red reflector',(2.274,s*.685,.338),(.012,.112,.025),RED,.008)
cube('Rear lower valance',(2.22,0,.277),(.12,1.49,.065),BLACK,.027)
# Right-hand exhaust, with a dark recessed throat.
cyl('Exhaust pipe',(1.94,-.61,.28),(2.32,-.61,.28),.056,BLACK,48)
cyl('Exhaust dark outlet',(2.323,-.61,.28),(2.325,-.61,.28),.048,RUBBER,48)
tube('Polished exhaust rolled lip',[(2.329,-.61+.054*cos(j*2*pi/48),.28+.054*sin(j*2*pi/48)) for j in range(48)],.006,CHROME,8,True)

# Pedestal wing: aerofoil cross-section, two swept uprights and endplates.
for s in [-1,1]:
    verts=[(x,s*y,z) for x,y,z in [(1.77,.57,.96),(1.91,.57,.96),(1.97,.57,1.23),(1.86,.57,1.23),(1.77,.595,.96),(1.91,.595,.96),(1.97,.595,1.23),(1.86,.595,1.23)]]
    mesh('Swept wing pedestal',verts,[(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],BLACK,.008)
    cube('Wing pedestal foot',(1.84,s*.583,.978),(.19,.075,.026),BLACK,.012)
    pts=[(1.76,1.225),(1.775,1.29),(2.053,1.314),(2.071,1.265),(2.06,1.216)]
    vv=[(x,s*y,z) for y in [.828,.844] for x,z in pts]
    mesh('Wing endplate',vv,[(0,1,2,3,4),(9,8,7,6,5)]+[(i,(i+1)%5,(i+1)%5+5,i+5) for i in range(5)],BLACK,.006)
def wing(u,v):
    x=1.765+.29*u;y=(2*v-1)*.835
    return(x,y,1.258+.028*sin(pi*u)-.012*u+.012*(abs(2*v-1)**3))
patch('Wing upper aerofoil',wing,16,32,BLACK)
patch('Wing lower aerofoil',lambda u,v:(wing(u,v)[0],wing(u,v)[1],1.249-.010*sin(pi*u)+.012*(abs(2*v-1)**3)),16,32,BLACK)
for u in [0,1]:tube('Wing rolled edge',[wing(u,j/48) for j in range(49)],.005,BLACK,8)

# Cabin: readable through the clear glazing; no unseen engine geometry.
cube('Cabin floor',(.25,0,.34),(2.5,1.43,.08),TRIM,.03)
cube('Dashboard',(-.74,0,.908),(.39,1.48,.20),TRIM,.065)
cube('Dashboard upper pad',(-.77,0,1.012),(.30,1.41,.033),TRIM,.016)
cube('Centre console',(-.02,0,.53),(1.1,.22,.29),TRIM,.04)
cube('Centre radio surround',(-.525,0,.84),(.025,.20,.18),BLACK,.01)
cube('Radio screen',(-.51,0,.874),(.01,.153,.056),GLASS,.005)
for y in [-.48,-.15,.15,.48]:cube('Dashboard air vent',(-.54,y,.969),(.018,.13,.035),BLACK,.007)
for s in [-1,1]:
    cube('Front seat cushion',(-.13,s*.39,.53),(.51,.47,.14),TRIM,.06)
    seat=cube('Front seat back',(.12,s*.39,.77),(.135,.46,.48),TRIM,.065)
    cube('Front headrest',(.13,s*.39,1.092),(.15,.245,.19),TRIM,.05)
    for a in [-1,1]:
        cube('Seat side bolster',(-.12,s*.39+a*.195,.58),(.46,.06,.15),TRIM,.025)
        cube('Seat back bolster',(.054,s*.39+a*.192,.81),(.095,.065,.37),TRIM,.025)
    cube('Rear headrest',(.99,s*.4,1.081),(.12,.23,.16),TRIM,.045)
    cube('Door inner panel',(.1,s*.782,.75),(1.96,.035,.36),TRIM,.026)
cube('Rear bench cushion',(.91,0,.56),(.48,1.35,.16),TRIM,.055)
cube('Rear bench back',(1.13,0,.80),(.16,1.35,.40),TRIM,.05)
cube('Rear parcel shelf',(1.205,0,1.0),(.29,1.43,.024),TRIM,.012)
# Left hand drive steering wheel lies in a tilted plane.
cy=-.38;cz=.928;cx=-.47
pts=[(cx+.032*sin(j*2*pi/64),cy+.158*cos(j*2*pi/64),cz+.15*sin(j*2*pi/64)) for j in range(64)]
tube('Steering wheel leather rim',pts,.017,TRIM,10,True)
cyl('Steering column',(-.7,cy,.82),(cx,cy,cz),.028,BLACK,24)
for a in [pi/6,5*pi/6,3*pi/2]:tube('Steering spoke',[(cx,cy,cz),(cx+.023*sin(a),cy+.143*cos(a),cz+.14*sin(a))],.014,BLACK,8)
cube('Steering airbag',(cx+.01,cy,cz),(.045,.10,.077),TRIM,.025)
tube('Gear lever',[(-.27,0,.675),(-.28,0,.79)],.012,BLACK,10)
cube('Gear knob',(-.28,0,.80),(.045,.042,.042),BLACK,.02)
cube('Rear view mirror',(-.76,0,1.293),(.045,.215,.073),BLACK,.014)
for s in [-1,1]:
    # Wiper arms sit directly above the windscreen's lower edge.
    tube('Wiper arm',[(-.934,s*.31,1.047),(-.874,s*.43,1.092),(-.848,s*.59,1.109)],.005,BLACK,6)
    tube('Wiper rubber blade',[(-.89,s*.28,1.094),(-.837,s*.64,1.113)],.007,BLACK,6)
cube('Undertray',(0,0,.235),(3.75,1.30,.035),BLACK,.02)

# Four separately pivoted road wheels: rounded sidewalls, tread sipes,
# five shaped spokes, lip, hub fasteners, ventilated disc and caliper.
for wx,axle in [(-1.365,'F'),(1.335,'R')]:
    for s,side in [(-1,'L'),(1,'R')]:
        parts=[];active_list=parts;wy=s*.781;wz=.325
        name='Wheel_'+axle+side
        # Symmetric profile, with a low sidewall and sculpted shoulder.
        prof=[(-.116,.237),(-.118,.267),(-.109,.299),(-.089,.318),(-.061,.325),(.061,.325),(.089,.318),(.109,.299),(.118,.267),(.116,.237)]
        lathe_y('Performance tyre',wx,wy,wz,prof,RUBBER,96)
        for sg in [-1,1]:
            for rr in [.246,.282,.297]:
                lathe_y('Sidewall moulding ring',wx,wy,wz,[(sg*.114,rr-.0013),(sg*.116,rr),(sg*.114,rr+.0013)],RUBBER,80)
        # Circumferential grooves and diagonal tread blocks catch grazing light.
        for d in [-.047,0,.047]:
            lathe_y('Tread longitudinal channel',wx,wy,wz,[(d-.002,.3252),(d,.3225),(d+.002,.3252)],RUBBER,96)
        for j in range(60):
            a=j*2*pi/60
            for sg in [-1,1]:
                pts=[]
                for k in range(5):
                    d=sg*(.012+.078*k/4);aa=a+sg*.055*k/4;r=.325-(max(0,abs(d)-.06))*.18
                    pts.append((wx+r*cos(aa),wy+d,wz+r*sin(aa)))
                tube('Tyre diagonal sipe',pts,.0018,BLACK,4)
        outer=s*.112
        # Barrel with an open centre, no opaque disc over the spokes.
        profile=[(-s*.097,.227),(s*.087,.227),(s*.109,.235),(s*.116,.237),(s*.119,.229),(s*.106,.220),(-s*.097,.220)]
        lathe_y('Alloy barrel and rolled rim',wx,wy,wz,profile,ALLOY,80)
        lathe_y('Machined rim edge',wx,wy,wz,[(s*.113,.233),(s*.12,.236),(s*.122,.232)],CHROME,80)
        # Brake disc is set behind the spokes.
        lathe_y('Brake rotor',wx,wy,wz,[(s*.044,.083),(s*.044,.184),(s*.054,.184),(s*.054,.083)],ALLOY,64)
        lathe_y('Rotor wear track',wx,wy,wz,[(s*.055,.11),(s*.055,.177)],CHROME,64)
        for j in range(24):
            a=j*2*pi/24
            for rr in [.139,.167]:
                cyl('Drilled rotor hole',(wx+rr*cos(a),wy+s*.056,wz+rr*sin(a)),(wx+rr*cos(a),wy+s*.0565,wz+rr*sin(a)),.0034,BLACK,8)
        cube('Brake caliper',(wx-.153,wy+s*.035,wz+.055),(.067,.07,.13),BLACK,.017)
        for j in range(5):
            a=pi/2+j*2*pi/5
            poly=[(.049,-.024),(.082,-.031),(.216,-.039),(.229,-.018),(.216,.016),(.089,.023)]
            verts=[]
            for dep in [s*.074,s*.111]:
                for r,t in poly:verts.append((wx+r*cos(a)-t*sin(a),wy+dep,wz+r*sin(a)+t*cos(a)))
            ob=mesh('Forged five spoke',verts,[tuple(range(5,-1,-1)),tuple(range(6,12))]+[(i,(i+1)%6,(i+1)%6+6,i+6) for i in range(6)],ALLOY,.005)
        cyl('Wheel centre hub',(wx,wy+s*.071,wz),(wx,wy+s*.115,wz),.070,ALLOY,48)
        cyl('Wheel centre cap',(wx,wy+s*.115,wz),(wx,wy+s*.119,wz),.033,BLACK,40)
        cyl('Wheel centre badge',(wx,wy+s*.119,wz),(wx,wy+s*.120,wz),.018,CHROME,32)
        for j in range(5):
            a=j*2*pi/5;x=wx+.048*cos(a);z=wz+.048*sin(a)
            cyl('Lug nut recess',(x,wy+s*.115,z),(x,wy+s*.117,z),.010,BLACK,16)
            cyl('Hex lug nut',(x,wy+s*.117,z),(x,wy+s*.122,z),.0067,CHROME,6)
        tube('Tyre valve',[(wx+.201,wy+s*.112,wz-.06),(wx+.208,wy+s*.128,wz-.062)],.004,RUBBER,8)
        wheels[name]={'parts':parts,'center':(wx,wy,wz)}
active_list=body

# Normals, applied manufacturing bevels, deterministic object names and UVs.
# Components stay editable in the .blend; export meshes are consolidated later.
import bmesh
for ob in list(model.objects):
    if ob.type!='MESH':continue
    applymods(ob)
    bm=bmesh.new();bm.from_mesh(ob.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001)
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.000001)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free()
    # Box projection in metres, useful for tiled detail and later texture work.
    uv=ob.data.uv_layers.new(name='UV0')
    for poly in ob.data.polygons:
        n=poly.normal;axis=max(range(3),key=lambda i:abs(n[i]));ax=[i for i in range(3) if i!=axis]
        for li in poly.loop_indices:
            co=ob.data.vertices[ob.data.loops[li].vertex_index].co;uv.data[li].uv=(co[ax[0]],co[ax[1]])
    # Split normals along only steep transitions, preserving curved panels.
    ob.data.set_sharp_from_angle(angle=math.radians(48))

def empty(name,collection=model):
    o=bpy.data.objects.new(name,None);collection.objects.link(o);return o
source_root=empty('Sedan01_EDITABLE')
source_root['reference']='Sedan01_Reference.png; original model, no placeholder meshes used'
source_root['dimensions_m']='4.66 x 2.14 mirrors x 1.474 height'
source_root['front']='Blender -Y; Unity +Z'
for ob in body:ob.parent=source_root
for name,item in wheels.items():
    e=empty(name+'__SOURCE');e.parent=source_root;e.location=xyz(item['center'])
    for ob in item['parts']:ob.parent=e;ob.matrix_parent_inverse=e.matrix_world.inverted()
    # matrix_world update is required before inverse assignment on a new parent.
    bpy.context.view_layer.update()
    for ob in item['parts']:
        ob.matrix_parent_inverse=e.matrix_world.inverted()

# Save original modelling file before constructing export variants.
scene['asset']='Sedan01 | independently modelled from supplied reference'
scene['units']='Metres. Ground contact z=0. Wheels radius .325m; wheelbase 2.700m.'

def joincopies(objects,name,collection,ratio=1):
    copies=[]
    for ob in objects:
        cp=ob.copy();cp.data=ob.data.copy();collection.objects.link(cp)
        cp.matrix_world=ob.matrix_world.copy();cp.parent=None;copies.append(cp)
    bpy.ops.object.select_all(action='DESELECT')
    for cp in copies:cp.select_set(True)
    bpy.context.view_layer.objects.active=copies[0];bpy.ops.object.join()
    o=copies[0];o.name=name
    if ratio<1:
        dec=o.modifiers.new('Game LOD reduction','DECIMATE');dec.ratio=ratio;applymods(o)
    # Triangulation frozen in exported files, editable quads remain in source.
    tri=o.modifiers.new('Deterministic game triangulation','TRIANGULATE');applymods(o)
    return o

export_collection=bpy.data.collections.new('GAME_EXPORTS');scene.collection.children.link(export_collection)
stats={}
for level,ratio in [(0,.70),(1,.28),(2,.08)]:
    root=empty('Sedan01',export_collection);export_objs=[root]
    # Glass is separated for correct render queues and independent sorting.
    solid=[o for o in body if o.data.materials[0]!=GLASS]
    glass=[o for o in body if o.data.materials[0]==GLASS]
    for parts,name in [(solid,'Body'),(glass,'Glass')]:
        o=joincopies(parts,name,export_collection,ratio);o.parent=root;export_objs.append(o)
    for name,item in wheels.items():
        o=joincopies(item['parts'],name,export_collection,ratio)
        # Bake centre as origin, giving useful local wheel rotation pivots.
        centre=Vector(xyz(item['center']))
        for v in o.data.vertices:v.co-=centre
        o.location=centre;o.parent=root;export_objs.append(o)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in export_objs:ob.select_set(True)
    bpy.context.view_layer.objects.active=root
    OUT.mkdir(parents=True,exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=str(OUT/f'Sedan01_LOD{level}.fbx'),use_selection=True,object_types={'MESH','EMPTY'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_space_transform=True,bake_space_transform=True,mesh_smooth_type='OFF',use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO')
    stats[f'LOD{level}']={'triangles':sum(len(o.data.polygons) for o in export_objs if o.type=='MESH'),'vertices':sum(len(o.data.vertices) for o in export_objs if o.type=='MESH'),'mesh_objects':6}
    for ob in export_objs:bpy.data.objects.remove(ob,do_unlink=True)
bpy.data.collections.remove(export_collection)
(SRC/'model_stats.json').write_text(json.dumps(stats,indent=2),encoding='utf8')
print('SEDAN01_STATS',json.dumps(stats),flush=True)

# Neutral photographic studio, excluded from game exports.
def studio_obj(ob):
    if ob in body:body.remove(ob)
    place(ob,studio);return ob
groundmat=mat('Studio warm grey',(.205,.215,.222),.05,.67)
ground=studio_obj(cube('Studio floor',(0,0,-.035),(200,200,.05),groundmat,0))
def area(name,pos,power,size,color):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;d.color=color
    o=bpy.data.objects.new(name,d);studio.objects.link(o);o.location=xyz(pos);o.rotation_euler=(Vector(xyz((0,0,.65)))-o.location).to_track_quat('-Z','Y').to_euler()
area('Large key softbox',(-3,-4,6),1050,5,(1.0,.95,.88))
area('Long roof strip',(1,1.5,5),1350,4,(.83,.91,1.0))
area('Front fill',(-4,3,2.5),700,4,(.95,.98,1.0))
area('Rear rim',(4,-2,3.5),1050,3,(1,.96,.91))
camera_data=bpy.data.cameras.new('Camera');camera=bpy.data.objects.new('Camera',camera_data);studio.objects.link(camera);scene.camera=camera
def view(pos,target,lens=54):
    camera.location=xyz(pos);camera.rotation_euler=(Vector(xyz(target))-camera.location).to_track_quat('-Z','Y').to_euler();camera_data.type='PERSP';camera_data.lens=lens
view((-6.5,-5.6,3.25),(0,0,.74),58)
# Store an immediate, pleasant opening view in the source blend.
for screen in bpy.data.screens:
    for a in screen.areas:
        if a.type=='VIEW_3D':
            a.spaces.active.region_3d.view_distance=6.6
            a.spaces.active.region_3d.view_location=xyz((0,0,.7))
            a.spaces.active.region_3d.view_rotation=camera.rotation_euler.to_quaternion()
            a.spaces.active.clip_end=200
bpy.ops.object.select_all(action='DESELECT')
source_root.select_set(True);bpy.context.view_layer.objects.active=source_root
scene.render.filepath=str(RENDERS/'Sedan01_Front34.png')
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'Sedan01.blend'))
bpy.ops.render.render(write_still=True)
view((6.3,-5.4,2.7),(0,0,.74),58)
scene.render.filepath=str(RENDERS/'Sedan01_Rear34.png');bpy.ops.render.render(write_still=True)
# Technical side view, useful for comparing the proportions to the reference.
view((0,-8,1.0),(0,0,.74));camera_data.type='ORTHO';camera_data.ortho_scale=5.55
scene.render.filepath=str(RENDERS/'Sedan01_Side.png');bpy.ops.render.render(write_still=True)
print('SEDAN01_COMPLETE',flush=True)
