using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Dungeon;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // The realm eases where the party has been (docs/PLAN_RUN_MAP.md section 1).
    //
    // One Image covers the whole leg with the painting again, drawn through the
    // MapEasing shader: it tiles the painting down the leg and shows it only
    // inside the discs below. The discs are rebuilt from the run on every
    // repaint -- cleared rooms and slain anchors are what the save already
    // holds -- so a loaded run eases exactly as it did when it was left.
    public partial class MapController
    {
        [SerializeField] internal Image easedWood;

        // Dimmed like the found strip (BackdropTint 0.5), a touch lighter so the
        // painted warmth reads as light coming back; the art carries the grade.
        private static readonly Color EasedTint = new Color(0.6f, 0.6f, 0.6f, 1f);

        private const string EasingShaderPath = "Shaders/MapEasing";

        private static readonly int DiscsId = Shader.PropertyToID("_Discs");
        private static readonly int DiscCountId = Shader.PropertyToID("_DiscCount");
        private static readonly int RectSizeId = Shader.PropertyToID("_RectSize");
        private static readonly int TileHeightId = Shader.PropertyToID("_TileHeight");

        private Material _easingMaterial;
        private readonly Vector4[] _discBuffer = new Vector4[MapEasing.ShaderCapacity];
        private List<EasingDisc> _easedDiscs = new List<EasingDisc>();

        // What the mask is made of right now, in content coordinates.
        public IReadOnlyList<EasingDisc> EasedDiscs => _easedDiscs;

        private void PaintEasing(MapGraph graph, RunSnapshot run, HashSet<int> cleared, RealmNode current)
        {
            if (easedWood == null) return;

            if (!EnsureEasingMaterial())
            {
                easedWood.gameObject.SetShown(false);
                _easedDiscs = new List<EasingDisc>();
                return;
            }

            // Walked rooms: everything cleared, and the entry once the party
            // has left it (it is never "cleared", see StateOf).
            var walked = graph.Nodes
                .Where(n => cleared.Contains(n.Id) || (n.Role == RealmNodeRole.Entry && n.Id != current.Id))
                .Select(n => graph.PositionOf(n.Id));

            int slain = run.slainAnchorIds?.Count ?? 0;
            _easedDiscs = MapEasing.Discs(walked, graph.HeartCentre, slain);

            for (int i = 0; i < _discBuffer.Length; i++)
            {
                _discBuffer[i] = i < _easedDiscs.Count
                    ? new Vector4(_easedDiscs[i].X, _easedDiscs[i].Y, _easedDiscs[i].Radius, 0f)
                    : Vector4.zero;
            }

            var rect = easedWood.rectTransform;
            rect.sizeDelta = new Vector2(MapLayout.BackgroundTileWidth, graph.ContentHeight);

            _easingMaterial.SetVectorArray(DiscsId, _discBuffer);
            _easingMaterial.SetFloat(DiscCountId, _easedDiscs.Count);
            _easingMaterial.SetVector(RectSizeId, new Vector4(rect.sizeDelta.x, rect.sizeDelta.y, 0f, 0f));
            _easingMaterial.SetFloat(TileHeightId, MapLayout.BackgroundTileHeight);

            easedWood.color = EasedTint;
            easedWood.gameObject.SetShown(_easedDiscs.Count > 0);
        }

        // Found by path, as the stage shaders are. A missing shader leaves the
        // wood as it was found, with a warning, instead of a full-strength
        // second painting.
        private bool EnsureEasingMaterial()
        {
            if (_easingMaterial != null) return true;

            var shader = Resources.Load<Shader>(EasingShaderPath);
            if (shader == null)
            {
                Debug.LogWarning($"MapController: no shader at Resources/{EasingShaderPath}; the realm will not ease");
                return false;
            }

            // An instance, so the disc array is this controller's and never
            // written into a shared asset.
            _easingMaterial = new Material(shader) { name = "MapEasing (instance)" };
            easedWood.material = _easingMaterial;
            return true;
        }

        private void ReleaseEasingMaterial()
        {
            if (_easingMaterial == null) return;
            Destroy(_easingMaterial);
            _easingMaterial = null;
        }
    }
}
