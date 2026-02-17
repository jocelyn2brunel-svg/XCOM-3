using Microsoft.Xna.Framework;

namespace XCOM_3
{
    /// <summary>
    /// Paramètres globaux de morphologie humaine, modifiables à runtime via l'éditeur de corps.
    /// Ces valeurs influencent les dimensions de rendu des unités humaines (soldats).
    /// </summary>
    public static class HumanBodyMorphSettings
    {
        // Échelles globales (appliquées au gabarit de base Soldier)
        public static float HeadScale = 1f;
        public static float TorsoWidthScale = 1f;
        public static float TorsoHeightScale = 1f;
        public static float TorsoDepthScale = 1f;
        public static float LimbWidthScale = 1f;
        public static float ArmLengthScale = 1f;
        public static float LegLengthScale = 1f;
        public static float ShoulderWidthScale = 1f;
        public static float ShoulderHeightScale = 1f;
        public static float ShoulderSlopeScale = 1f;
        public static float ShoulderSphereScale = 1f;

        // Ajustements du profil féminin
        public static float FeminineHeadScale = 0.98f;
        public static float FeminineTorsoWidthScale = 0.90f;
        public static float FeminineTorsoDepthScale = 0.85f;
        public static float FeminineLimbWidthScale = 0.90f;
        public static float FeminineTorsoHeightScale = 0.95f;
        public static float FeminineLegLengthScale = 1.05f;

        // Ratios de structure du torse
        public static float RibRatio = 1f;
        public static float AbdomenRatio = 0.8f;
        public static float PelvisRatio = 1f;

        // Facteurs de forme du torse selon le sexe
        public static float FeminineRibTopWidthFactor = 1.0f;
        public static float MasculineRibTopWidthFactor = 1.08f;
        public static float FeminineRibBottomWidthFactor = 0.9f;
        public static float MasculineRibBottomWidthFactor = 0.84f;
        public static float FemininePelvisTopWidthFactor = 1.06f;
        public static float MasculinePelvisTopWidthFactor = 1.02f;
        public static float FemininePelvisBottomWidthFactor = 0.9f;
        public static float MasculinePelvisBottomWidthFactor = 0.84f;

        public static void ResetDefaults()
        {
            HeadScale = 1f;
            TorsoWidthScale = 1f;
            TorsoHeightScale = 1f;
            TorsoDepthScale = 1f;
            LimbWidthScale = 1f;
            ArmLengthScale = 1f;
            LegLengthScale = 1f;
            ShoulderWidthScale = 1f;
            ShoulderHeightScale = 1f;
            ShoulderSlopeScale = 1f;
            ShoulderSphereScale = 1f;

            FeminineHeadScale = 0.98f;
            FeminineTorsoWidthScale = 0.90f;
            FeminineTorsoDepthScale = 0.85f;
            FeminineLimbWidthScale = 0.90f;
            FeminineTorsoHeightScale = 0.95f;
            FeminineLegLengthScale = 1.05f;

            RibRatio = 1f;
            AbdomenRatio = 0.8f;
            PelvisRatio = 1f;

            FeminineRibTopWidthFactor = 1.0f;
            MasculineRibTopWidthFactor = 1.08f;
            FeminineRibBottomWidthFactor = 0.9f;
            MasculineRibBottomWidthFactor = 0.84f;
            FemininePelvisTopWidthFactor = 1.06f;
            MasculinePelvisTopWidthFactor = 1.02f;
            FemininePelvisBottomWidthFactor = 0.9f;
            MasculinePelvisBottomWidthFactor = 0.84f;
        }

        public static float ClampScale(float value, float min = 0.5f, float max = 1.6f)
            => MathHelper.Clamp(value, min, max);

        public static float ClampRatio(float value, float min = 0.4f, float max = 1.8f)
            => MathHelper.Clamp(value, min, max);
    }
}
