namespace _8floor.TimeManagement.Core.Scripts.Runtime.CollectionRoom
{
    public interface ICollectionRoomItemView : IReactToHover
    {
        public string Name { get; }
        public bool IsOpened { get; }

        public void Initialize(ICollectionRoomController collectionRoomController, bool isOpened);
    }
}
