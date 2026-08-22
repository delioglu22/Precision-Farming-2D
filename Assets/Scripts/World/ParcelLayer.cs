using UnityEngine;
using UnityEngine.U2D;

/// <summary>Which part of the slab a layer traces.</summary>
public enum ParcelFace
{
    /// <summary>The top face, optionally pulled in from the outline.</summary>
    Top,
    /// <summary>The side that falls away under the lower left edge.</summary>
    LowerLeft,
    /// <summary>The side that falls away under the lower right edge.</summary>
    LowerRight
}

/// <summary>
/// One of the fills a parcel is stacked from.
///
/// A parcel is a slab, so it needs more than a flat shape: an outline, a border,
/// a crop, and the two sides that fall away under the lower edges. Those two sides
/// catch the light differently and meeting at the bottom corner is what makes the
/// parcel read as solid, so they are separate layers rather than one skirt.
///
/// Insetting the outline rather than the fill (SpriteShape's own <c>fillOffset</c>)
/// is what keeps the corners sharp - an offset fill rounds them off.
/// </summary>
[RequireComponent(typeof(SpriteShapeController))]
[DisallowMultipleComponent]
public class ParcelLayer : MonoBehaviour
{
    [Tooltip("Which part of the slab this layer draws.")]
    [SerializeField] ParcelFace face = ParcelFace.Top;

    [Tooltip("Top faces only: how far the outline is pulled inside the parcel.")]
    [SerializeField, Min(0f)] float inset;

    [Tooltip("Side faces only: how far the slab falls away, in world units.")]
    [SerializeField, Min(0f)] float depth = 0.2f;

    public ParcelFace Face { get { return face; } }
    public float Inset { get { return inset; } }
    public float Depth { get { return depth; } }
}
