using UnityEngine;
using Unity.Mathematics;
public class UnityChanControllerInput : AnimationInputBase<UnityChanControllerData> {
    public float DirectionStep = 0.1f;
    public float SpeedStep = 0.1f;

    protected override void UpdateComponentData(ref UnityChanControllerData data) {
        data.Player = 1;
        var deltaDir = Input.GetAxisRaw("Horizontal");
        var deltaSpeed = Input.GetAxisRaw("Vertical");
        data.Direction = math.clamp(data.Direction + deltaDir * DirectionStep, 0.0f, 2.0f);
        data.Speed = math.clamp(data.Speed + deltaSpeed * SpeedStep, 0.0f, 1.0f);
    }
}
