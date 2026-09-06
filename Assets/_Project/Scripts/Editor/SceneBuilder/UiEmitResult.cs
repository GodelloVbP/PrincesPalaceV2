using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.UiKit;

// What the emitter hands back: the map from declared node to emitted object,
// plus typed accessors the wiring step uses.
//
// This is Contract B's front door. A screen's Wire step reads objects out of
// here and assigns controller fields DIRECTLY, so a typo or a type mismatch is
// a compile error at the wiring site rather than a null field discovered three
// screens later.
public sealed class UiEmitResult
{
    private readonly Dictionary<UiNode, GameObject> _objects = new Dictionary<UiNode, GameObject>();

    public IReadOnlyDictionary<UiNode, GameObject> Objects => _objects;

    // Every controller the Wire step attached, so the wiring sweep can check
    // their serialized fields without being told where to look.
    public readonly List<Component> AttachedControllers = new List<Component>();

    internal void Record(UiNode node, GameObject go) => _objects[node] = go;

    public GameObject Go(NodeRef reference)
    {
        if (!reference.IsValid)
        {
            throw new System.InvalidOperationException(
                "Wiring used an empty NodeRef. The screen kept a handle to a node it never added to the tree.");
        }

        if (!_objects.TryGetValue(reference.Node, out var go))
        {
            throw new System.InvalidOperationException(
                $"Wiring asked for '{reference.Node.Name}' ({reference.Node.Kind}) but it was never emitted. " +
                $"Either it is not in the tree this screen returned, or it sits under a node that was skipped.");
        }

        NoteHandedOut(go, reference.Node, viaChildren: false);
        return go;
    }

    // Each of the five is a PAIR of overloads rather than one method with an
    // optional `searchChildren = false`, and that is not a style choice: a
    // dozen Wire lines pass these as method groups (`.Select(result.Tmp)`),
    // and a method group whose only candidate has a trailing optional
    // parameter matches Func<T,int,TResult> as well as Func<T,TResult>, so
    // Select's type inference stops working at every one of them. Two
    // overloads, second parameter a bool, keeps both readings unambiguous.
    //
    // `searchChildren` itself is the opt-in for the ONE shape that needs it:
    // a Ui.Button's caption is a generated `<Name>Label` child, so a line
    // that wants the text of a button rather than of a label node has to say
    // so. Descending was the unconditional fallback of every typed lookup
    // until F9, which meant any of them could quietly return a component
    // from a different object than the node named -- and nothing downstream
    // could tell that apart from a correct binding. Two sites in
    // ScreenRegistry ask for it; everything else resolves the node itself or
    // throws naming it.
    public TMP_Text Tmp(NodeRef reference) => Require<TMP_Text>(reference, searchChildren: false);

    public TMP_Text Tmp(NodeRef reference, bool searchChildren) => Require<TMP_Text>(reference, searchChildren);

    public Button Button(NodeRef reference) => Require<Button>(reference, searchChildren: false);

    public Button Button(NodeRef reference, bool searchChildren) => Require<Button>(reference, searchChildren);

    public Image Image(NodeRef reference) => Require<Image>(reference, searchChildren: false);

    public Image Image(NodeRef reference, bool searchChildren) => Require<Image>(reference, searchChildren);

    public RectTransform Rect(NodeRef reference) => Require<RectTransform>(reference, searchChildren: false);

    public RectTransform Rect(NodeRef reference, bool searchChildren) =>
        Require<RectTransform>(reference, searchChildren);

    // Engine dressing: procedural-sprite assigners, ambient animators, anything
    // whose type Domain cannot name. Bounded and labelled on purpose -- in v1
    // everything was an escape hatch; here this is the only one, and every use
    // is greppable.
    public T Attach<T>(NodeRef reference) where T : Component
    {
        var component = Go(reference).AddComponent<T>();
        NoteHandedOut(component, reference.Node, viaChildren: false);
        // Recorded so the wiring sweep can check this component's serialized
        // fields without being told where to look. Attaching through
        // AddComponent directly would bypass that check silently.
        AttachedControllers.Add(component);
        return component;
    }

    private T Require<T>(NodeRef reference, bool searchChildren) where T : Component
    {
        var go = Go(reference);

        var component = go.GetComponent<T>();
        if (component == null && searchChildren)
        {
            component = go.GetComponentInChildren<T>(includeInactive: true);
        }

        if (component == null)
        {
            throw new System.InvalidOperationException(
                $"'{reference.Node.Name}' is a {reference.Node.Kind} and has no {typeof(T).Name} on the node " +
                "itself. Check the node kind: only Ui.Label carries text on its own object, and only " +
                "Ui.Button carries a Button. A Ui.Button's caption is a generated child object, so a line " +
                "that genuinely wants it must say `searchChildren: true` and be read as doing so.");
        }

        NoteHandedOut(component, reference.Node, viaChildren: component.gameObject != go);
        return component;
    }

    // ---- what this result handed out, and for which node ------------------
    //
    // The seam the binding audit reads. Wiring goes through the five typed
    // accessors above and nowhere else, so recording here covers all ~115
    // explicit Wire lines without touching one of them -- and it is the only
    // place that knows which NodeRef a given component came out of, which is
    // exactly the fact "is this field bound to the right node" needs and which
    // the field itself cannot answer afterwards.
    public readonly struct Provenance
    {
        public readonly UiNode Node;

        // True only for a lookup that opted into searchChildren, so the audit
        // can hold an exact resolution to identity and a descended one to
        // "somewhere under that node".
        public readonly bool ViaChildren;

        public Provenance(UiNode node, bool viaChildren)
        {
            Node = node;
            ViaChildren = viaChildren;
        }
    }

    private readonly Dictionary<Object, Provenance> _handedOut = new Dictionary<Object, Provenance>();

    public bool TryProvenanceOf(Object value, out Provenance provenance) =>
        _handedOut.TryGetValue(value, out provenance);

    private void NoteHandedOut(Object value, UiNode node, bool viaChildren)
    {
        if (value == null) return;

        // First wins. A node handed out twice is the same node both times;
        // recording the second would only matter if the first were wrong.
        if (!_handedOut.ContainsKey(value)) _handedOut[value] = new Provenance(node, viaChildren);
    }

    // ---- what UiAutoBind filled, and from where ---------------------------

    public sealed class AutoBound
    {
        public Component Controller;
        public string FieldName;

        // -1 for a single field; the element's position for an array one, so
        // a mis-ordered array is reported at the element rather than as "this
        // field is wrong somewhere".
        public int Index;

        public UiNode Node;
    }

    // Recorded by UiAutoBind as it binds, so the audit can check an auto-bound
    // field against the node the NAME RULE chose rather than against the node
    // the value happens to sit on -- the one comparison that can catch the
    // rule matching something unintended.
    public readonly List<AutoBound> AutoBoundFields = new List<AutoBound>();
}
