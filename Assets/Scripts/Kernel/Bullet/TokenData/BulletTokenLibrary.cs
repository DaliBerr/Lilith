using System.Collections.Generic;
using UnityEngine;

namespace Kernel.Bullet
{
    /// <summary>
    /// 维护一份用于 Token Select 弹窗的可选 token 库。
    /// </summary>
    [CreateAssetMenu(menuName = "Lilith/Bullet Tokens/Bullet Token Library", fileName = "BulletTokenLibrary")]
    public sealed class BulletTokenLibrary : ScriptableObject
    {
        [SerializeField] private List<PlaceableTokenData> selectableTokens = new();

        /// <summary>
        /// summary: 返回当前库中已清理后的可选 token 列表。
        /// param: 无
        /// returns: 只读 token 列表
        /// </summary>
        public IReadOnlyList<PlaceableTokenData> SelectableTokens
        {
            get
            {
                SanitizeTokens();
                return selectableTokens;
            }
        }

        /// <summary>
        /// summary: 用一组新的 token 集合替换当前库内容，并自动去重。
        /// param name="tokens": 需要写入库中的 token 集合
        /// returns: 无
        /// </summary>
        public void SetTokens(IEnumerable<PlaceableTokenData> tokens)
        {
            selectableTokens ??= new List<PlaceableTokenData>();
            selectableTokens.Clear();
            AppendUniqueTokens(tokens);
        }

        /// <summary>
        /// summary: 向当前库补充一个 token；若已存在则忽略。
        /// param name="token": 需要加入库中的 token
        /// returns: 无
        /// </summary>
        public void AddToken(PlaceableTokenData token)
        {
            if (token == null)
            {
                return;
            }

            selectableTokens ??= new List<PlaceableTokenData>();
            if (ContainsToken(token))
            {
                return;
            }

            selectableTokens.Add(token);
        }

        /// <summary>
        /// summary: 返回当前库中的 token 视图，供 UI 采样器直接读取。
        /// param: 无
        /// returns: 清理后的 token 只读列表
        /// </summary>
        public IReadOnlyList<PlaceableTokenData> GetTokens()
        {
            SanitizeTokens();
            return selectableTokens;
        }

        private void OnValidate()
        {
            SanitizeTokens();
        }

        private void AppendUniqueTokens(IEnumerable<PlaceableTokenData> tokens)
        {
            if (tokens == null)
            {
                return;
            }

            foreach (PlaceableTokenData token in tokens)
            {
                AddToken(token);
            }
        }

        private bool ContainsToken(PlaceableTokenData token)
        {
            if (token == null || selectableTokens == null)
            {
                return false;
            }

            int tokenId = token.GetInstanceID();
            for (int i = 0; i < selectableTokens.Count; i++)
            {
                PlaceableTokenData existing = selectableTokens[i];
                if (existing != null && existing.GetInstanceID() == tokenId)
                {
                    return true;
                }
            }

            return false;
        }

        private void SanitizeTokens()
        {
            selectableTokens ??= new List<PlaceableTokenData>();
            HashSet<int> seenInstanceIds = new();
            for (int i = selectableTokens.Count - 1; i >= 0; i--)
            {
                PlaceableTokenData token = selectableTokens[i];
                if (token == null || !seenInstanceIds.Add(token.GetInstanceID()))
                {
                    selectableTokens.RemoveAt(i);
                }
            }
        }
    }
}
