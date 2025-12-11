using System;
using ExhaustiveSwitch;
using UnityEngine;

namespace ExhaustiveSwitchSamples.AdvancedPatterns
{
    // ===== ステートマシンパターン =====

    /// <summary>
    /// 敵のAI状態を表すインターフェース
    /// </summary>
    [Exhaustive]
    public interface IEnemyState
    {
        string StateName { get; }
    }

    /// <summary>
    /// 待機状態
    /// </summary>
    [Case]
    public sealed class IdleState : IEnemyState
    {
        public string StateName => "待機";
        public float IdleTime { get; set; }

        public IdleState(float idleTime = 0f)
        {
            IdleTime = idleTime;
        }
    }

    /// <summary>
    /// 巡回状態
    /// </summary>
    [Case]
    public sealed class PatrolState : IEnemyState
    {
        public string StateName => "巡回";
        public Vector3[] PatrolPoints { get; set; }
        public int CurrentPointIndex { get; set; }

        public PatrolState(Vector3[] patrolPoints)
        {
            PatrolPoints = patrolPoints;
            CurrentPointIndex = 0;
        }
    }

    /// <summary>
    /// 追跡状態
    /// </summary>
    [Case]
    public sealed class ChaseState : IEnemyState
    {
        public string StateName => "追跡";
        public Transform Target { get; set; }
        public float ChaseSpeed { get; set; }

        public ChaseState(Transform target, float chaseSpeed = 5f)
        {
            Target = target;
            ChaseSpeed = chaseSpeed;
        }
    }

    /// <summary>
    /// 攻撃状態
    /// </summary>
    [Case]
    public sealed class AttackState : IEnemyState
    {
        public string StateName => "攻撃";
        public Transform Target { get; set; }
        public float LastAttackTime { get; set; }
        public float AttackCooldown { get; set; }

        public AttackState(Transform target, float attackCooldown = 1.5f)
        {
            Target = target;
            AttackCooldown = attackCooldown;
            LastAttackTime = 0f;
        }
    }

    /// <summary>
    /// 逃走状態
    /// </summary>
    [Case]
    public sealed class FleeState : IEnemyState
    {
        public string StateName => "逃走";
        public Vector3 FleeDirection { get; set; }
        public float FleeSpeed { get; set; }

        public FleeState(Vector3 fleeDirection, float fleeSpeed = 7f)
        {
            FleeDirection = fleeDirection;
            FleeSpeed = fleeSpeed;
        }
    }

    /// <summary>
    /// 敵のAIステートマシン
    /// ExhaustiveSwitchですべての状態を確実に処理
    /// </summary>
    public class EnemyStateMachine
    {
        private IEnemyState currentState;
        private Transform enemyTransform;
        private float currentHP;
        private float maxHP;

        public EnemyStateMachine(Transform transform, float hp = 100f)
        {
            enemyTransform = transform;
            maxHP = hp;
            currentHP = hp;
            currentState = new IdleState();
        }

        /// <summary>
        /// 状態を更新
        /// </summary>
        public void Update(Transform playerTransform, float deltaTime)
        {
            // 現在の状態に応じた処理
            switch (currentState)
            {
                case IdleState idle:
                    UpdateIdle(idle, playerTransform, deltaTime);
                    break;

                case PatrolState patrol:
                    UpdatePatrol(patrol, playerTransform, deltaTime);
                    break;

                case ChaseState chase:
                    UpdateChase(chase, playerTransform, deltaTime);
                    break;

                case AttackState attack:
                    UpdateAttack(attack, playerTransform, deltaTime);
                    break;

                case FleeState flee:
                    UpdateFlee(flee, deltaTime);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(currentState), currentState, null);
            }
        }

        /// <summary>
        /// 状態遷移の条件判定
        /// </summary>
        public void CheckTransitions(Transform playerTransform)
        {
            float distanceToPlayer = Vector3.Distance(enemyTransform.position, playerTransform.position);
            float hpPercentage = currentHP / maxHP;

            // HPが低い場合は逃走
            if (hpPercentage < 0.2f && !(currentState is FleeState))
            {
                TransitionTo(new FleeState((enemyTransform.position - playerTransform.position).normalized));
                return;
            }

            // 現在の状態に応じた遷移判定
            switch (currentState)
            {
                case IdleState idle when distanceToPlayer < 10f:
                    TransitionTo(new ChaseState(playerTransform));
                    break;

                case PatrolState patrol when distanceToPlayer < 8f:
                    TransitionTo(new ChaseState(playerTransform));
                    break;

                case ChaseState _ when distanceToPlayer < 2f:
                    TransitionTo(new AttackState(playerTransform));
                    break;

                case ChaseState _ when distanceToPlayer > 15f:
                    TransitionTo(new IdleState());
                    break;

                case AttackState _ when distanceToPlayer > 3f:
                    TransitionTo(new ChaseState(playerTransform));
                    break;

                case FleeState _ when hpPercentage > 0.5f:
                    TransitionTo(new IdleState());
                    break;

                default:
                    // 遷移なし
                    break;
            }
        }

        /// <summary>
        /// 状態遷移
        /// </summary>
        private void TransitionTo(IEnemyState newState)
        {
            Debug.Log($"状態遷移: {currentState.StateName} → {newState.StateName}");
            currentState = newState;
        }

        // 各状態の更新処理
        private void UpdateIdle(IdleState idle, Transform player, float deltaTime)
        {
            idle.IdleTime += deltaTime;
            if (idle.IdleTime > 3f)
            {
                // 待機時間が長くなったら巡回に移行
                TransitionTo(new PatrolState(new[] { enemyTransform.position + Vector3.forward * 5, enemyTransform.position + Vector3.right * 5 }));
            }
        }

        private void UpdatePatrol(PatrolState patrol, Transform player, float deltaTime)
        {
            Vector3 targetPoint = patrol.PatrolPoints[patrol.CurrentPointIndex];
            enemyTransform.position = Vector3.MoveTowards(enemyTransform.position, targetPoint, 2f * deltaTime);

            if (Vector3.Distance(enemyTransform.position, targetPoint) < 0.1f)
            {
                patrol.CurrentPointIndex = (patrol.CurrentPointIndex + 1) % patrol.PatrolPoints.Length;
            }
        }

        private void UpdateChase(ChaseState chase, Transform player, float deltaTime)
        {
            Vector3 direction = (chase.Target.position - enemyTransform.position).normalized;
            enemyTransform.position += direction * chase.ChaseSpeed * deltaTime;
        }

        private void UpdateAttack(AttackState attack, Transform player, float deltaTime)
        {
            attack.LastAttackTime += deltaTime;
            if (attack.LastAttackTime >= attack.AttackCooldown)
            {
                Debug.Log("攻撃!");
                attack.LastAttackTime = 0f;
            }
        }

        private void UpdateFlee(FleeState flee, float deltaTime)
        {
            enemyTransform.position += flee.FleeDirection * flee.FleeSpeed * deltaTime;
        }

        public void TakeDamage(float damage)
        {
            currentHP -= damage;
            Debug.Log($"ダメージ: {damage}, 残りHP: {currentHP}/{maxHP}");
        }
    }
}
