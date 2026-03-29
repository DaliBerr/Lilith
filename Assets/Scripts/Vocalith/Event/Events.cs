using System.Collections.Generic;
// using Vocalith.Tick;
using Vocalith.UI;
using UnityEngine;

namespace Vocalith.EventSystem
{
    public static class EventManager
    {
        public static readonly EventBus eventBus = new();
    }
    public static partial class EventList{
    public readonly struct MapReady
    {
        public readonly bool value;
        // public readonly Vector3 mapCenterPosition;
        public MapReady(bool value )
        {
            this.value = value;
            // this.mapCenterPosition = mapCenterPosition;
        }
    }
    public readonly struct MainSceneInitialized
    {
        public readonly bool isInitialized;
        public MainSceneInitialized(bool isInitialized)
        {
            this.isInitialized = isInitialized;
        }
    }

    public readonly struct ItemLoaded
    {
        public readonly int itemCount;
        public ItemLoaded(int itemCount)
        {
            this.itemCount = itemCount;

        }
    }

    public readonly struct BuildingLoaded
    {
        public readonly int buildingCount;
        public BuildingLoaded(int buildingCount)
        {
            this.buildingCount = buildingCount;
        }
    }

    public struct SettingChanged
    {
        public bool needApply;
        public SettingChanged(bool needApply)
        {
            this.needApply = needApply;
        }
    }

    public struct CancelSettingChange
    {
        public List<string> undoSetting;
        public CancelSettingChange(List<string> undoSetting)
        {
            this.undoSetting = undoSetting;
        }
    }

    public struct SaveGameRequest
    {
        public string saveName;
        public SaveGameRequest(string saveName)
        {
            this.saveName = saveName;
        }
    }

    public struct LoadGameRequest
    {
        public string loadName;
        public LoadGameRequest(string loadName)
        {
            this.loadName = loadName;
        }
    }

    public struct CloseModalRequest
    {
        public UIScreen modalUI;
        public CloseModalRequest(UIScreen modalUI)
        {
            this.modalUI = modalUI;
        }

    }

    public struct SelectedFactory
    {
        public long buildingId;
        public SelectedFactory(long buildingId)
        {
        this.buildingId = buildingId;
        }
    }

    public struct TryModifyInteriorBuildingEvent
    {
        public string buildingId;
        public bool isAdd;
        public TryModifyInteriorBuildingEvent(string buildingId, bool isAdd)
        {
            this.buildingId = buildingId;
            this.isAdd = isAdd;
        }
    }
    

    public struct FactoryGridSelected
    {
        public int gridIndex;
        public bool isEmpty;
        public FactoryGridSelected(int gridIndex, bool isEmpty)
        {
            this.gridIndex = gridIndex;
            this.isEmpty = isEmpty;
        }

    }
}
}
