using System.Collections.Generic;
using UnityEngine;

namespace Kernel.MapGrid
{
    [CreateAssetMenu(fileName = "RoomGenerationProfileLibrary", menuName = "Lilith/Map/Room Generation Profile Library")]
    public sealed class RoomGenerationProfileLibrary : ScriptableObject
    {
        [SerializeField] private RoomGenerationProfileData startProfile;
        [SerializeField] private List<RoomGenerationProfileData> combatProfiles = new();
        [SerializeField] private RoomGenerationProfileData rewardProfile;
        [SerializeField] private RoomGenerationProfileData bossProfile;

        public bool TrySelectProfile(RoomGenerationInput input, out RoomGenerationProfileData profile, out string error)
        {
            profile = null;
            error = null;

            switch (input.RoomKind)
            {
                case RoomKind.Start:
                    return TryUseSingleProfile(input.RoomKind, startProfile, "Start", out profile, out error);
                case RoomKind.Reward:
                    return TryUseSingleProfile(input.RoomKind, rewardProfile, "Reward", out profile, out error);
                case RoomKind.Boss:
                    return TryUseSingleProfile(input.RoomKind, bossProfile, "Boss", out profile, out error);
                case RoomKind.Combat:
                default:
                    return TrySelectCombatProfile(input, out profile, out error);
            }
        }

        private bool TrySelectCombatProfile(RoomGenerationInput input, out RoomGenerationProfileData profile, out string error)
        {
            profile = null;
            error = null;
            if (combatProfiles == null || combatProfiles.Count == 0)
            {
                error = "RoomGenerationProfileLibrary is missing Combat profiles.";
                return false;
            }

            for (int i = 0; i < combatProfiles.Count; i++)
            {
                if (combatProfiles[i] != null)
                {
                    continue;
                }

                error = $"RoomGenerationProfileLibrary Combat profile slot {i} is empty.";
                return false;
            }

            int selectedIndex = (int)((uint)input.Seed % (uint)combatProfiles.Count);
            return TryUseSingleProfile(RoomKind.Combat, combatProfiles[selectedIndex], "Combat", out profile, out error);
        }

        private static bool TryUseSingleProfile(
            RoomKind expectedKind,
            RoomGenerationProfileData candidate,
            string label,
            out RoomGenerationProfileData profile,
            out string error)
        {
            profile = candidate;
            error = null;
            if (profile == null)
            {
                error = $"RoomGenerationProfileLibrary is missing the {label} profile.";
                return false;
            }

            if (profile.RoomKind == expectedKind)
            {
                return true;
            }

            error = $"RoomGenerationProfile '{profile.name}' is configured for {profile.RoomKind}, but {expectedKind} was requested.";
            profile = null;
            return false;
        }
    }
}
