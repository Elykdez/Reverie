"""Rebuild the island's static OBJ. Requires numpy, scipy and Pillow."""
from pathlib import Path
import numpy as np
from PIL import Image
from scipy.interpolate import PchipInterpolator
from scipy.ndimage import gaussian_filter, map_coordinates

folder = Path(__file__).resolve().parents[1] / 'Assets/Environment/RockIsland'
height = np.asarray(Image.open(folder / 'rock_08_disp_2k.jpg').convert('L'), dtype=float) / 255
# Fine grain belongs in the relief shader, not sparsely sampled vertex spikes.
height = gaussian_filter(height, sigma=4, mode='wrap')
segments, layers = 256, 96
# Resample a rounded square by arc length, preserving the grass patch footprint.
angle = np.linspace(0, 2 * np.pi, 4097)
outline = 2.02 * np.stack((np.sign(np.cos(angle)) * np.abs(np.cos(angle)) ** (1/3),
                         np.sign(np.sin(angle)) * np.abs(np.sin(angle)) ** (1/3)), axis=1)
arc = np.r_[0, np.cumsum(np.linalg.norm(np.diff(outline, axis=0), axis=1))]
u = np.linspace(0, 1, segments + 1)
outline = np.stack([np.interp(u * arc[-1], arc, outline[:, k]) for k in range(2)], axis=1)
outline[-1] = outline[0]
radial = outline / np.linalg.norm(outline, axis=1)[:, None]
rim_depth = .14
rim_end = rim_depth / 2.64
profile = PchipInterpolator([rim_end, .16, .4, .68, .88, 1], [1, 1.008, .90, .66, .32, .045])
rim_angles = np.linspace(0, np.pi / 2, 9)
rings = [(rim_end * (1 - np.cos(a)), .96 + .04 * np.sin(a)) for a in rim_angles]
rings += [(t, profile(t)) for t in np.linspace(rim_end, 1, layers + 1)[1:]]
layers = len(rings) - 1
vertices, uv = [], []
rim_height = .018 * np.sin(2*np.pi*u*5) + .012 * np.sin(2*np.pi*u*11 + .7) + .005 * np.sin(2*np.pi*u*23)
rim_height[-1] = rim_height[0]
rim_distance = np.zeros_like(u)
previous_rim = None
for t, radius in rings:
    y = -.16 - 2.64 * t + rim_height * np.exp(-(t/.14)**2)
    rim_fade = np.clip(t / .12, 0, 1)
    fade = rim_fade * rim_fade * (3 - 2 * rim_fade) * np.sin(np.pi * (.08 + .92 * t)) ** .5
    crags = (.10 * np.sin(2*np.pi*u*7 + .6*np.sin(t*5))
             + .035 * np.sin(2*np.pi*u*19 + t*9)
             + .012 * np.sin(2*np.pi*u*37 - t*13)) * fade
    # Follow surface distance around the bevel instead of collapsing its UVs onto height.
    rim_points = np.column_stack((outline[:, 0] * radius, y, outline[:, 1] * radius))
    if t <= rim_end:
        if previous_rim is not None:
            rim_distance += np.linalg.norm(rim_points - previous_rim, axis=1)
        previous_rim = rim_points
    tex_u = u * 10
    tex_v = .16 / 1.5 + (rim_distance + max(0, t-rim_end) * 2.64) / 1.5
    detail = map_coordinates(height, [(1-tex_v % 1)*(height.shape[0]-1),
                                      (tex_u % 1)*(height.shape[1]-1)], order=1, mode='wrap')
    displacement = crags + (detail - .5) * .08 * fade
    ring = outline * radius + radial * displacement[:, None]
    for i, (x, z) in enumerate(ring):
        vertices.append((x + .06*np.sin(t*4)*t, y[i], z + .04*np.sin(t*7)*t))
        uv.append((tex_u[i], tex_v[i]))

faces = []
stride = segments + 1
for layer in range(layers):
    for i in range(segments):
        a = layer * stride + i
        b, c, d = a+1, a+stride, a+stride+1
        # Alternating diagonals avoid continuous triangulation patterns.
        if (i+layer) % 2:
            faces.extend([(a,b,d), (a,d,c)])
        else:
            faces.extend([(a,b,c), (b,d,c)])
# Continue the side UVs inward over concentric top rings; the central UV pinch is hidden by grass.
seam_pairs = [(layer*stride, layer*stride+segments) for layer in range(layers+1)]
top_outline = np.asarray(vertices[:stride])
previous_ring = 0
for radius in np.linspace(1, .08, 13)[1:]:
    ring_start = len(vertices)
    for i, point in enumerate(top_outline):
        blend = radius * radius * (3 - 2 * radius)
        vertices.append((point[0]*radius, -.16 + rim_height[i]*blend, point[2]*radius))
        uv.append((u[i]*10, .16/1.5 - np.linalg.norm(point[[0, 2]])*(1-radius)/1.5))
    for i in range(segments):
        a, b, c, d = previous_ring+i, previous_ring+i+1, ring_start+i, ring_start+i+1
        faces.extend([(a,b,c), (b,d,c)])
    seam_pairs.append((ring_start, ring_start+segments))
    previous_ring = ring_start
center_id = len(vertices)
vertices.append((0,-.16,0)); uv.append((5,-1.4))
for i in range(segments):
    faces.append((center_id, previous_ring+i, previous_ring+i+1))

# The underside keeps its planar UV cap.
for ring_layer, center in [(layers, (.06*np.sin(4),-2.86,.04*np.sin(7)))]:
    center_id = len(vertices)
    vertices.append(center); uv.append((.5,.5))
    ring_start = len(vertices)
    for i in range(stride):
        point = vertices[ring_layer*stride+i]
        vertices.append(point); uv.append((point[0]/1.5,point[2]/1.5))
    for i in range(segments):
        face = (center_id, ring_start+i+1, ring_start+i)
        faces.append(tuple(reversed(face)))

vertices = np.asarray(vertices)
faces = np.asarray(faces)
# Orient closed-surface triangles away from an interior point.
normals = np.cross(vertices[faces[:,1]]-vertices[faces[:,0]], vertices[faces[:,2]]-vertices[faces[:,0]])
centers = vertices[faces].mean(axis=1) - np.array([0,-1.2,0])
flip = (normals * centers).sum(axis=1) < 0
faces[flip] = faces[flip][:,[0,2,1]]
normals = np.cross(vertices[faces[:,1]]-vertices[faces[:,0]], vertices[faces[:,2]]-vertices[faces[:,0]])
smooth = np.zeros_like(vertices)
for k in range(3): np.add.at(smooth, faces[:,k], normals)
for a, b in seam_pairs:
    smooth[a] = smooth[b] = smooth[a] + smooth[b]
smooth /= np.maximum(np.linalg.norm(smooth, axis=1, keepdims=True), 1e-10)

# Bake the approved size into geometry; keep UVs and unit normals unchanged.
vertices *= 1.2
lines = ['# Reverie island: rounded stone relief; intrinsic size, import scale 1.0.', 'o RockyIsland', 's 1']
lines += ['v %.6f %.6f %.6f' % tuple(v) for v in vertices]
lines += ['vt %.6f %.6f' % tuple(v) for v in uv]
lines += ['vn %.6f %.6f %.6f' % tuple(v) for v in smooth]
lines += ['f ' + ' '.join(f'{i+1}/{i+1}/{i+1}' for i in face) for face in faces]
assert np.all(np.linalg.norm(normals, axis=1) > 1e-9), 'Degenerate triangles'
assert np.all(np.isfinite(smooth)), 'Invalid normals'
# Weld UV duplicates for topology validation; every edge of the closed island has two faces.
_, welded = np.unique(np.round(vertices, 6), axis=0, return_inverse=True)
welded_faces = welded[faces]
edges = np.sort(np.concatenate([welded_faces[:, [0, 1]], welded_faces[:, [1, 2]], welded_faces[:, [2, 0]]]), axis=1)
assert np.all(np.unique(edges, axis=0, return_counts=True)[1] == 2), 'Non-manifold island'
(folder / 'RockyIsland.obj').write_text('\n'.join(lines) + '\n')
print(f'{len(vertices)} vertices; {len(faces)} triangles; rounded rim; smooth seam normals; watertight')
