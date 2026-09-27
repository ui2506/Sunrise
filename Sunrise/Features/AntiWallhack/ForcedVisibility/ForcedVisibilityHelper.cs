using System.Diagnostics.CodeAnalysis;
using LabApi.Features.Wrappers;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp049;
using PlayerRoles.PlayableScps.Scp049.Zombies;
using PlayerRoles.PlayableScps.Scp106;
using PlayerRoles.PlayableScps.Scp173;
using PlayerRoles.PlayableScps.Scp939;
using PlayerRoles.PlayableScps.Scp939.Mimicry;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps.Scp096;

namespace Sunrise.Features.AntiWallhack.ForcedVisibility;

internal static class ForcedVisibilityHelper
{
    public static float GetForcedVisibility(Player player) => player.RoleBase switch
    {
        Scp939Role scp939 => Get939Visibility(scp939),
        Scp096Role scp096 => Get096Visibility(scp096),
        Scp106Role scp106 => Get106Visibility(scp106),
        Scp049Role or Scp173Role or ZombieRole => 100, // Those guys can be heard from more then 36m away

        HumanRole humanRole => GetHumanVisibility(player, humanRole),
        _ => 12,
    };

    static float Get939Visibility(Scp939Role scp939)
    {
        // breathe - 15
        // breathe when lunging - 7
        // jump 30
        // sprint >50

        float result = GetMovementInfo(scp939, out bool isJumping) switch
        {
            MovementState.Sprinting => 100,
            _ => (scp939.SubroutineModule.TryGetSubroutine(out Scp939FocusAbility focus) && focus.TargetState) ? 7f : 15f,
        };

        if (isJumping)
            result = Mathf.Max(result, 30);
        else if (scp939.SubroutineModule.TryGetSubroutine(out EnvironmentalMimicry mimicry) && !mimicry.Cooldown.IsReady)
            result = Mathf.Max(result, 30);

        return result;
    }

    static float Get096Visibility(Scp096Role scp096) => scp096.StateController.AbilityState switch
    {
        // crying - 30
        // trying not to cry - 4
        // enraged max

        Scp096AbilityState.None => 30,
        Scp096AbilityState.TryingNotToCry => 4,
        _ => 100,
    };

    static float Get106Visibility(Scp106Role scp106)
    {
        // breathe - 7
        // move - 33
        // jump - 33

        float result = GetMovementInfo(scp106, out bool isJumping) switch
        {
            MovementState.Walking => 33,
            _ => 7,
        };

        if (isJumping)
            result = Mathf.Max(result, 33);

        return result;
    }

    static float GetHumanVisibility(Player player, HumanRole humanRole)
    {
        float result = GetMovementInfo(humanRole, out bool isJumping) switch
        {
            MovementState.Sprinting => 16,
            MovementState.Walking => 8,
            _ => 0,
        };

        if (player.VoiceModule?.ServerIsSending == true)
            result = Mathf.Max(result, 22);
        else if (isJumping)
            result = Mathf.Max(result, 8);

        result = Mathf.Max(result, ItemVisibilityHelper.GetVisibilityRange(player.CurrentItem));

        return result;
    }

    static MovementState GetMovementInfo(IFpcRole fpcRole, out bool isJumping)
    {
        FirstPersonMovementModule movementModule = fpcRole.FpcModule;

        isJumping = movementModule.Motor.JumpController.IsJumping || (AntiWallhackModule.LandingTimes.TryGetValue(Player.Get(movementModule.Hub), out float landingTime) && Time.time - landingTime < 0.5f);

        return movementModule.CurrentMovementState switch
        {
            PlayerMovementState.Walking => movementModule.Motor.Velocity == Vector3.zero ? MovementState.Stationary : MovementState.Walking,
            _ => (MovementState)movementModule.CurrentMovementState,
        };
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    enum MovementState
    {
        Crouching,
        Sneaking,
        Walking,
        Sprinting,

        Stationary,
    }
}
