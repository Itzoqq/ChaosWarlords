namespace ChaosWarlords.Source.Core.Interfaces.Data
{
    /// <summary>
    /// Contract for Data Transfer Objects to ensure bidirectional conversion.
    /// </summary>
    /// <typeparam name="TEntity">The domain entity type this DTO represents.</typeparam>
    public interface IDto<TEntity>
    {
        /// <summary>
        /// Converts the DTO back into the Domain Entity.
        /// </summary>
        /// <returns>Isomorphic entity.</returns>
        TEntity ToEntity();
    }
}
