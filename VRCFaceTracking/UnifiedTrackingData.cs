using System;
using System.Collections.Generic;
using System.Linq;
using ViveSR.anipal.Eye;
using ViveSR.anipal.Lip;
using VRCFaceTracking.Params;
using VRCFaceTracking.Params.Eye;
using VRCFaceTracking.Params.LipMerging;
using Vector2 = VRCFaceTracking.Params.Vector2;

namespace VRCFaceTracking
{
    // Represents a single eye, can also be used as a combined eye
    public struct Eye
    {
        public Vector2 Look;
        public float Openness;
        public float Widen, Squeeze;

        
        public void Update(SingleEyeData eyeData, SingleEyeExpression? expression = null)
        {
            if (eyeData.GetValidity(SingleEyeDataValidity.SINGLE_EYE_DATA_GAZE_DIRECTION_VALIDITY))
                Look = eyeData.gaze_direction_normalized.Invert();

            Openness = eyeData.eye_openness;
            
            if (expression == null) return; // This is null when we use this as a combined eye, so don't try read data from it
            
            Widen = expression.Value.eye_wide;
            Squeeze = expression.Value.eye_squeeze;
        }
    }
    
    public class EyeTrackingData
    {
        // Camera Data
        public (int x, int y) ImageSize;
        public byte[] ImageData;
        public bool SupportsImage;
        
        public Eye Left, Right, Combined;
        
        // SRanipal Exclusive
        public float EyesDilation;
        private float _maxDilation, _minDilation;


        public void UpdateData(EyeData_v2 eyeData)
        {
            float dilation = 0;
            
            if (eyeData.verbose_data.right.GetValidity(SingleEyeDataValidity
                .SINGLE_EYE_DATA_PUPIL_DIAMETER_VALIDITY))
            {
                dilation = eyeData.verbose_data.right.pupil_diameter_mm;
                UpdateMinMaxDilation(eyeData.verbose_data.right.pupil_diameter_mm);
            }
            else if (eyeData.verbose_data.left.GetValidity(SingleEyeDataValidity
                .SINGLE_EYE_DATA_PUPIL_DIAMETER_VALIDITY))
            {
                dilation = eyeData.verbose_data.left.pupil_diameter_mm;
                UpdateMinMaxDilation(eyeData.verbose_data.left.pupil_diameter_mm);
            }

            Left.Update(eyeData.verbose_data.left, eyeData.expression_data.left);
            Right.Update(eyeData.verbose_data.right, eyeData.expression_data.right);
            
            Combined.Update(eyeData.verbose_data.combined.eye_data);
            // Fabricate missing combined eye data
            Combined.Widen = (Left.Widen + Right.Widen) / 2;
            Combined.Squeeze = (Left.Squeeze + Right.Squeeze) / 2;
            
            if (dilation != 0)
                EyesDilation = dilation / _minDilation / (_maxDilation - _minDilation);
        }

        private void UpdateMinMaxDilation(float readDilation)
        {
            if (readDilation > _maxDilation)
                _maxDilation = readDilation;
            if (readDilation < _minDilation)
                _minDilation = readDilation;
        }

        public void ResetThresholds()
        {
            _maxDilation = 0;
            _minDilation = 999;
        }

        public object[] LeftRightPitchYaw()
        {
            //(In degrees) left pitch, left yaw, right pitch, right yaw. Example data: -14.903, 23.592, -15.560, 16.503
            return new object[]
            {
                Lerp((Left.Look.y + 1.0f) * 0.5f, MinPitch, MaxPitch),
                Lerp((Left.Look.x + 1.0f) * 0.5f, YawOuter, YawInner), //-1 is left (outer), 1 is right (inner)
                Lerp((Right.Look.y + 1.0f) * 0.5f, MinPitch, MaxPitch),
                Lerp((Right.Look.x + 1.0f) * 0.5f, -YawInner, -YawOuter), //-1 is left (outer), 1 is right (inner), SO swap and negate so -1 is inner right and 1 is outer left
            };
        }

        private float Lerp(float t, float min, float max)
            => min + (max - min) * t;
        public float MinPitch { get; set; } = 60.0f;
        public float MaxPitch { get; set; } = -60.0f;
        public float YawOuter { get; set; } = -100.0f;
        public float YawInner { get; set; } = 100.0f;
    }

    public class LipTrackingData
    {
        // Camera Data
        public (int x, int y) ImageSize;
        public byte[] ImageData;
        public bool SupportsImage;

        public float[] LatestShapes = new float[SRanipal_Lip_v2.WeightingCount];

        public void UpdateData(LipData_v2 lipData)
        {
            unsafe
            {
                for (int i = 0; i < SRanipal_Lip_v2.WeightingCount; i++)
                    LatestShapes[i] = lipData.prediction_data.blend_shape_weight[i];
            }
        }
    }

    public class UnifiedTrackingData
    {
        public static readonly List<IParameter> AllParameters = EyeTrackingParams.ParameterList.Union(LipShapeMerger.AllLipParameters).ToList();

        // Central update action for all parameters to subscribe to
        public static Action<EyeTrackingData /* Lip Data Blend Shape  */
            , LipTrackingData /* Lip Weightings */> OnUnifiedDataUpdated;

        public static EyeTrackingData LatestEyeData = new EyeTrackingData();
        public static LipTrackingData LatestLipData = new LipTrackingData();
    }
}