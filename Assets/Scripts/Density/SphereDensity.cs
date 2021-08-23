using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SphereDensity", menuName = "Density Generator/Sphere")]
public class SphereDensity : DensityGenerator
{
    [Range(0.0001f,1f)]
    public float radius = 1;

    public override float[] Generate () {
        densityShader.SetFloat ("radius", radius);
        return base.Generate ();
    }
}