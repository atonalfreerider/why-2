using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Why.Economy.Model
{
    /// <summary>
    /// Builds the <see cref="WorldModel"/> in every economy-scene run, whichever view is shown: it reads global.json as the
    /// scene starts and parses it on a worker thread (once per run: the model is shared), then logs on the main thread,
    /// where the smoke test counts errors, the "[Why] World ..." lines (the blocks, shares, row and computed order; the
    /// lanes and the share they carry; the conflict anchors; every 2026 item drawn or excluded; the events and what
    /// expires, all as of the data's own date) and each of <see cref="WorldModel.Problems"/> as an error, so a broken
    /// rule fails the run. The world's layers poll <see cref="WorldModel.Get"/>, which never blocks, until it returns the
    /// model; none calls <see cref="WorldModel.Provide"/> on the main thread, where a first build would parse in the
    /// frame. Idle once it has logged.
    /// </summary>
    [GraphScenes(GraphScene.Economy)]
    public sealed class WorldModelLoader : GraphModule
    {
        Task<WorldModel> build;

        public override void Init(GraphRoot root)
        {
            // built in an earlier visit to the scene: log it again for this one
            WorldModel built = WorldModel.Get();
            if (built != null)
            {
                build = Task.FromResult(built);
                return;
            }

            TextAsset asset = Resources.Load<TextAsset>(WorldModel.Path);
            if (asset == null)
            {
                Debug.LogError($"[Why] World: no '{WorldModel.Path}' resource (Assets/Resources/Data/economy/global.json): the world view has no data");
                enabled = false;
                return;
            }

            string json = asset.text;
            Resources.UnloadAsset(asset);
            build = Task.Run(() => WorldModel.Provide(json));
        }

        void Update()
        {
            if (build == null || !build.IsCompleted) return;
            enabled = false;
            if (build.IsFaulted || build.IsCanceled)
            {
                Debug.LogError("[Why] World: global.json could not be read: " + (build.Exception?.GetBaseException().ToString() ?? "cancelled"));
                return;
            }

            WorldModel m = build.Result;
            foreach (string line in m.Summary) Debug.Log("[Why] World " + line);
            foreach (string problem in m.Problems()) Debug.LogError("[Why] World check failed: " + problem);
        }
    }
}
