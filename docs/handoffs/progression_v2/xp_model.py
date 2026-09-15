import math

# ---------------------------------------------------------------
# Content facts (enemies.json)
# ---------------------------------------------------------------
# active enemies only (active: true), id -> (expReward, minFloor)
ACTIVE_NORMAL = {
    "rat": (15, 1),
    "golem": (35, 5),
    "bog_witch": (22, 2),
    "beetle": (24, 1),
    "treant": (40, 2),
}
ACTIVE_BOSS = {
    "forest_warden": (100, 1),
}

FIGHT_SHARE = 0.546  # measured share of ROLLED (non-forced) rooms that are Fight, DescentMap.cs:178-186
ELITE_REWARD_MULT = 1.56  # VictoryRewards.EliteRewardMultiplier
LEG_LENGTH = 8  # DescentMapGenerator.DefaultLegLength
ELITE_OFFSET = 4  # room offset (1-based within leg) forced Elite
BOSS_OFFSET = 8  # room offset forced Boss
ROLLED_OFFSETS = [1, 2, 3, 5, 6, 7]  # assumes restBeforeBoss == false (pre level-30 reward-track unlock)

NORMAL_MEAN_ENEMY_COUNT = 1.5  # NextInt(1,3): uniform over {1,2}
ELITE_ENEMY_COUNT = 2


def pool_mean(floor):
    vals = [exp for exp, minfloor in ACTIVE_NORMAL.values() if minfloor <= floor]
    return sum(vals) / len(vals)


def floor_for_leg_start(leg_start_step):
    if leg_start_step <= 0:
        return 1
    return leg_start_step // LEG_LENGTH + 1


def raw_multiplier(step, permille, max_scaled_step=200, max_multiplier=4_000_000.0):
    if step <= 0:
        return 1.0
    clamped = min(step, max_scaled_step)
    raw = (1.0 + permille / 1000.0) ** clamped
    return min(raw, max_multiplier)


def scale_reward(amount, step, permille):
    """DifficultyCurve.ScaleReward == ScaleHealth, floored. amount may be float (expectation)."""
    if amount == 0 or step <= 0:
        return amount
    return math.floor(amount * raw_multiplier(step, permille))


# ---------------------------------------------------------------
# Part A / B: per-fight-type average XP at a given depth step,
# and full leg-by-leg run totals.
# ---------------------------------------------------------------

def normal_fight_avg_xp(step, floor, permille):
    raw = NORMAL_MEAN_ENEMY_COUNT * pool_mean(floor)
    return scale_reward(raw, step, permille)


def elite_fight_avg_xp(step, floor, permille):
    raw = ELITE_ENEMY_COUNT * pool_mean(floor) * ELITE_REWARD_MULT
    return scale_reward(raw, step, permille)


def boss_fight_avg_xp(step, permille):
    raw = list(ACTIVE_BOSS.values())[0][0]  # only one active boss: forest_warden, exp 100
    return scale_reward(raw, step, permille)


def leg_expected_xp(leg_number, permille):
    """Expected XP from leg `leg_number` (1-based), summing room-by-room
    at the room's own absolute step (legStart+offset), as VictoryRewards.For
    actually scales per FightSession.Outcome.DepthStep = run.step (the room's
    own step), not the leg's start step."""
    leg_start = (leg_number - 1) * LEG_LENGTH
    floor = floor_for_leg_start(leg_start)
    total = 0.0
    for offset in range(1, LEG_LENGTH + 1):
        step = leg_start + offset
        if offset == ELITE_OFFSET:
            total += elite_fight_avg_xp(step, floor, permille)
        elif offset == BOSS_OFFSET:
            total += boss_fight_avg_xp(step, permille)
        elif offset in ROLLED_OFFSETS:
            total += FIGHT_SHARE * normal_fight_avg_xp(step, floor, permille)
        # non-fight rolled outcome contributes 0 XP
    return total


def cumulative_after_leg(n_legs, permille):
    return sum(leg_expected_xp(k, permille) for k in range(1, n_legs + 1))


# ---------------------------------------------------------------
# Part C: level curve fitting
# ---------------------------------------------------------------

def cost(level, c, g):
    return c * (g ** level)


def total_cost_to_level(target_level, c, g):
    """XP from level 1 to target_level = sum_{L=2}^{target_level} cost(L)."""
    return sum(cost(L, c, g) for L in range(2, target_level + 1))


def first_run_early_xp(permille):
    """XP from 6 consecutive assumed 'normal fights' at steps 0..5,
    floor 1 pool (rat, beetle only) -- the anchor's own simplification."""
    floor = 1
    total = 0.0
    for step in range(0, 6):
        total += normal_fight_avg_xp(step, floor, permille)
    return total


def fit_curve(permille, deep_run_total_xp, cap=40, level3_target_fights=6):
    target_lvl3_xp = first_run_early_xp(permille)  # must equal cost(2)+cost(3)
    target_lvl40_xp = 25 * deep_run_total_xp        # must equal sum_{L=2}^{40} cost(L)

    def c_from_g(g):
        denom = g ** 2 + g ** 3
        return target_lvl3_xp / denom

    def residual(g):
        c = c_from_g(g)
        return total_cost_to_level(cap, c, g) - target_lvl40_xp

    # bisection on g in (1.0, 3.0)
    lo, hi = 1.0001, 3.0
    r_lo, r_hi = residual(lo), residual(hi)
    # widen hi if needed
    while r_lo * r_hi > 0 and hi < 50:
        hi *= 1.5
        r_hi = residual(hi)

    for _ in range(200):
        mid = (lo + hi) / 2
        r_mid = residual(mid)
        if r_lo * r_mid <= 0:
            hi = mid
            r_hi = r_mid
        else:
            lo = mid
            r_lo = r_mid

    g = (lo + hi) / 2
    c = c_from_g(g)
    return c, g, target_lvl3_xp, target_lvl40_xp


# ---------------------------------------------------------------
# Run and print everything
# ---------------------------------------------------------------
if __name__ == "__main__":
    print("=== Part A/B: per-fight-type average XP by depth step (checkpoint = room's own step) ===")
    for permille, label in [(75, "75 permille (today)"), (40, "40 permille"), (25, "25 permille")]:
        print(f"\n-- {label} --")
        print(f"{'step':>5} {'floor':>5} {'normal':>10} {'elite':>10} {'boss':>10}")
        for step in range(0, 81, 8):
            floor = floor_for_leg_start(step)
            n = normal_fight_avg_xp(step, floor, permille)
            e = elite_fight_avg_xp(step, floor, permille)
            b = boss_fight_avg_xp(step, permille)
            print(f"{step:>5} {floor:>5} {n:>10.1f} {e:>10.1f} {b:>10.1f}")

    print("\n=== Run totals (cumulative XP at end of leg N) ===")
    for permille, label in [(75, "75 permille (today)"), (40, "40 permille"), (25, "25 permille")]:
        print(f"\n-- {label} --")
        for n_legs in [2, 3, 5, 7, 10]:
            total = cumulative_after_leg(n_legs, permille)
            print(f"  leg {n_legs:>2}: cumulative XP = {total:,.0f}")

    print("\n=== Part C: fitted level curves (cap 40) ===")
    deep_run_xp = {}
    for permille in [75, 40, 25]:
        deep_run_xp[permille] = cumulative_after_leg(10, permille)

    fits = {}
    for permille in [75, 40, 25]:
        c, g, t3, t40 = fit_curve(permille, deep_run_xp[permille])
        fits[permille] = (c, g)
        print(f"\npermille={permille}: deep-run XP={deep_run_xp[permille]:,.0f}  "
              f"target lvl3 xp(6 fights)={t3:,.1f}  target lvl40 cumulative xp={t40:,.0f}")
        print(f"  fitted c={c:.4f} g={g:.6f}")
        # residual check
        got3 = cost(2, c, g) + cost(3, c, g)
        got40 = total_cost_to_level(40, c, g)
        print(f"  check: cost(2)+cost(3)={got3:,.2f} (target {t3:,.2f}); "
              f"sum(2..40)={got40:,.0f} (target {t40:,.0f})")

    print("\n=== Per-level cost tables ===")
    for permille in [75, 40, 25]:
        c, g = fits[permille]
        drx = deep_run_xp[permille]
        print(f"\n-- permille={permille} (c={c:.3f}, g={g:.5f}) --")
        print(f"{'L':>3} {'cost(XP)':>14} {'cost(frac deep run)':>20}")
        for L in [2, 5, 10, 15, 20, 25, 30, 35, 40]:
            xp = cost(L, c, g)
            print(f"{L:>3} {xp:>14,.0f} {xp / drx:>20.3f}")

    def level_reached(total_xp, c, g, cap=40):
        acc = 0.0
        level = 1
        for L in range(2, cap + 1):
            need = cost(L, c, g)
            if acc + need > total_xp:
                return level
            acc += need
            level = L
        return cap

    print("\n=== Career progression (level reached), per scaling ===")
    for permille in [75, 40, 25]:
        c, g = fits[permille]
        drx = deep_run_xp[permille]
        leg2 = cumulative_after_leg(2, permille)
        leg3 = cumulative_after_leg(3, permille)
        leg5 = cumulative_after_leg(5, permille)
        leg10 = drx

        print(f"\n-- permille={permille} --")
        total = leg2
        print(f"  (a) after run 1 (dies leg 2):                     total_xp={total:,.0f}  level={level_reached(total, c, g)}")
        total += leg3
        print(f"  (b) + run 2 (dies leg 3):                         total_xp={total:,.0f}  level={level_reached(total, c, g)}")
        total += leg5
        print(f"  (c) + run 3 (dies leg 5):                         total_xp={total:,.0f}  level={level_reached(total, c, g)}")
        prefix = total
        total += leg10
        print(f"  (d) + run 4 (first deep run, leg 10):             total_xp={total:,.0f}  level={level_reached(total, c, g)}")
        print(f"  (e) cumulative after N total deep runs (prefix={prefix:,.0f} + N*{leg10:,.0f}):")
        for n_deep in [2, 5, 10, 15, 20, 25]:
            t = prefix + n_deep * leg10
            print(f"      N={n_deep:>2}: total_xp={t:,.0f}  level={level_reached(t, c, g)}")
