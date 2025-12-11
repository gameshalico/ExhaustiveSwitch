using System;
using System.Collections.Generic;
using ExhaustiveSwitch;
using UnityEngine;

namespace ExhaustiveSwitchSamples.AdvancedPatterns
{
    // ===== コマンドパターン（Undo/Redo対応） =====

    /// <summary>
    /// 実行可能なコマンドを表すインターフェース
    /// </summary>
    [Exhaustive]
    public interface IUndoableCommand
    {
        string CommandName { get; }
        void Execute();
        void Undo();
    }

    /// <summary>
    /// オブジェクト移動コマンド
    /// </summary>
    [Case]
    public sealed class MoveObjectCommand : IUndoableCommand
    {
        public string CommandName => "オブジェクト移動";

        private Transform target;
        private Vector3 previousPosition;
        private Vector3 newPosition;

        public MoveObjectCommand(Transform target, Vector3 newPosition)
        {
            this.target = target;
            this.previousPosition = target.position;
            this.newPosition = newPosition;
        }

        public void Execute()
        {
            target.position = newPosition;
            Debug.Log($"移動: {previousPosition} → {newPosition}");
        }

        public void Undo()
        {
            target.position = previousPosition;
            Debug.Log($"移動を取り消し: {newPosition} → {previousPosition}");
        }
    }

    /// <summary>
    /// オブジェクト回転コマンド
    /// </summary>
    [Case]
    public sealed class RotateObjectCommand : IUndoableCommand
    {
        public string CommandName => "オブジェクト回転";

        private Transform target;
        private Quaternion previousRotation;
        private Quaternion newRotation;

        public RotateObjectCommand(Transform target, Quaternion newRotation)
        {
            this.target = target;
            this.previousRotation = target.rotation;
            this.newRotation = newRotation;
        }

        public void Execute()
        {
            target.rotation = newRotation;
            Debug.Log($"回転: {previousRotation.eulerAngles} → {newRotation.eulerAngles}");
        }

        public void Undo()
        {
            target.rotation = previousRotation;
            Debug.Log($"回転を取り消し");
        }
    }

    /// <summary>
    /// オブジェクトスケールコマンド
    /// </summary>
    [Case]
    public sealed class ScaleObjectCommand : IUndoableCommand
    {
        public string CommandName => "オブジェクトスケール";

        private Transform target;
        private Vector3 previousScale;
        private Vector3 newScale;

        public ScaleObjectCommand(Transform target, Vector3 newScale)
        {
            this.target = target;
            this.previousScale = target.localScale;
            this.newScale = newScale;
        }

        public void Execute()
        {
            target.localScale = newScale;
            Debug.Log($"スケール: {previousScale} → {newScale}");
        }

        public void Undo()
        {
            target.localScale = previousScale;
            Debug.Log($"スケールを取り消し");
        }
    }

    /// <summary>
    /// オブジェクト削除コマンド
    /// </summary>
    [Case]
    public sealed class DeleteObjectCommand : IUndoableCommand
    {
        public string CommandName => "オブジェクト削除";

        private GameObject target;
        private bool isActive;

        public DeleteObjectCommand(GameObject target)
        {
            this.target = target;
            this.isActive = target.activeSelf;
        }

        public void Execute()
        {
            target.SetActive(false);
            Debug.Log($"オブジェクトを削除: {target.name}");
        }

        public void Undo()
        {
            target.SetActive(isActive);
            Debug.Log($"オブジェクトを復元: {target.name}");
        }
    }

    /// <summary>
    /// コマンド履歴管理クラス
    /// </summary>
    public class CommandHistory
    {
        private Stack<IUndoableCommand> undoStack = new Stack<IUndoableCommand>();
        private Stack<IUndoableCommand> redoStack = new Stack<IUndoableCommand>();

        /// <summary>
        /// コマンドを実行して履歴に追加
        /// </summary>
        public void ExecuteCommand(IUndoableCommand command)
        {
            command.Execute();
            undoStack.Push(command);
            redoStack.Clear(); // 新しいコマンドを実行したらRedoスタックをクリア

            LogCommandExecution(command);
        }

        /// <summary>
        /// 最後のコマンドを取り消し
        /// </summary>
        public void Undo()
        {
            if (undoStack.Count == 0)
            {
                Debug.LogWarning("取り消すコマンドがありません");
                return;
            }

            IUndoableCommand command = undoStack.Pop();
            command.Undo();
            redoStack.Push(command);

            Debug.Log($"Undo: {command.CommandName}");
        }

        /// <summary>
        /// 取り消したコマンドを再実行
        /// </summary>
        public void Redo()
        {
            if (redoStack.Count == 0)
            {
                Debug.LogWarning("やり直すコマンドがありません");
                return;
            }

            IUndoableCommand command = redoStack.Pop();
            command.Execute();
            undoStack.Push(command);

            Debug.Log($"Redo: {command.CommandName}");
        }

        /// <summary>
        /// コマンド実行のログ
        /// すべてのコマンドタイプを処理
        /// </summary>
        private void LogCommandExecution(IUndoableCommand command)
        {
            switch (command)
            {
                case MoveObjectCommand move:
                    Debug.Log($"[履歴] 移動コマンドを実行");
                    break;

                case RotateObjectCommand rotate:
                    Debug.Log($"[履歴] 回転コマンドを実行");
                    break;

                case ScaleObjectCommand scale:
                    Debug.Log($"[履歴] スケールコマンドを実行");
                    break;

                case DeleteObjectCommand delete:
                    Debug.Log($"[履歴] 削除コマンドを実行");
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(command), command, null);
            }
        }

        /// <summary>
        /// コマンドの詳細情報を取得
        /// </summary>
        public string GetCommandInfo(IUndoableCommand command)
        {
            switch (command)
            {
                case MoveObjectCommand _:
                    return "位置を変更するコマンド";

                case RotateObjectCommand _:
                    return "回転を変更するコマンド";

                case ScaleObjectCommand _:
                    return "スケールを変更するコマンド";

                case DeleteObjectCommand _:
                    return "オブジェクトを削除するコマンド";

                default:
                    throw new ArgumentOutOfRangeException(nameof(command), command, null);
            }
        }

        /// <summary>
        /// 履歴をクリア
        /// </summary>
        public void Clear()
        {
            undoStack.Clear();
            redoStack.Clear();
            Debug.Log("コマンド履歴をクリアしました");
        }

        public int UndoCount => undoStack.Count;
        public int RedoCount => redoStack.Count;
    }
}
