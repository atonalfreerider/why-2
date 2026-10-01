using System.Text;
using Why.Economy.Data;

namespace Why.Economy.Model
{
    /// <summary>
    /// Everything the economy scene's layers share, published by <see cref="Layers.EconomyLoaderLayer"/> under
    /// <see cref="SharedKey"/>: the data, the money circuit (built per year on demand) and the economic lives of the
    /// simulated population (run once the population exists, see <see cref="EconomicLives"/>).
    /// </summary>
    public sealed class EconomyModel
    {
        public const string SharedKey = "economy.model";

        public readonly EconomyData Data;
        public readonly MoneyCircuit Circuit;
        public readonly EconomicLives Lives;

        /// <summary>Calibration and validation notes written while building (shown in the Unity console).</summary>
        public readonly StringBuilder Log = new StringBuilder();

        public EconomyModel(EconomyData data, MoneyCircuit circuit, EconomicLives lives)
        {
            Data = data;
            Circuit = circuit;
            Lives = lives;
        }
    }
}
