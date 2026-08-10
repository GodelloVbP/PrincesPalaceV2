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

        return go;
    }

    public TMP_Text Tmp(NodeRef reference) => Require<TMP_Text>(reference);

    public Button Button(NodeRef reference) => Require<Button>(reference);

    public Image Image(NodeRef reference) => Require<Image>(reference);

    public RectTransform Rect(NodeRef reference) => Require<RectTransform>(reference);

    // Engine dressing: procedural-sprite assigners, ambient animators, anything
    // whose type Domain cannot name. Bounded and labelled on purpose -- in v1
    // everything was an escape hatch; here this is the only one, and every use
    // is greppable.
    public T Attach<T>(NodeRef reference) where T : Component
    {
        var component = Go(reference).AddComponent<T>();
        // Recorded so the wiring sweep can check this component's serialized
        // fields without being told where to look. Attaching through
        // AddComponent directly would bypass that check silently.
        AttachedControllers.Add(component);
        return component;
    }

    private T Require<T>(NodeRef reference) where T : Component
    {
        var go = Go(reference);

        // A Button's label is a CHILD, so look down one level before giving up -
        // otherwise every button label would need its own NodeRef purely to be
        // reachable.
        var component = go.GetComponent<T>() ?? go.GetComponentInChildren<T>(includeInactive: true);
        if (component == null)
        {
            throw new System.InvalidOperationException(
                $"'{reference.Node.Name}' is a {reference.Node.Kind} and has no {typeof(T).Name}. " +
                $"Check the node kind: only Ui.Label/Ui.Button carry text, and only Ui.Button carries a Button.");
        }

        return component;
    }
}
