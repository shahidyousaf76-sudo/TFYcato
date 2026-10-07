import { WorldDef } from './types';

export const WORLDS: WorldDef[] = [
  {
    id: 'park', name: 'Green Park', order: 1, difficultyLabel: 'Easy', enabled: true, comingSoon: false,
    music: 'music_park', ambience: ['amb_birds', 'amb_wind'], weather: 'petals',
    palette: { skyTop: '#5FB8FF', skyHorizon: '#CFEFFF', ground: '#6CCB5F', path: '#E8C98F', pathEdge: '#C9A56A', accent: '#FF7AB6', fog: '#DDF2FF', water: '#5EC8E8' },
    fogDensity: 0.006, decor: ['tree_round', 'flowers', 'bench', 'small_bridge', 'puddle', 'bush', 'lamp_post'],
    landmarks: ['leaning_tower_pisa', 'windmill', 'taj_mahal'],
  },
  {
    id: 'forest', name: 'Forest', order: 2, difficultyLabel: 'Easy–Medium', enabled: true, comingSoon: false,
    music: 'music_forest', ambience: ['amb_forest', 'amb_birds', 'amb_stream'], weather: 'leaves',
    palette: { skyTop: '#4F9FD8', skyHorizon: '#BEE3D0', ground: '#3E9A4A', path: '#9C7A52', pathEdge: '#6E5236', accent: '#FFD166', fog: '#CFE8D6', water: '#4FB3C9' },
    fogDensity: 0.012, decor: ['tree_tall', 'pine', 'rock_moss', 'stream', 'wood_bridge', 'fallen_tree', 'mushroom', 'butterflies', 'birds'],
    landmarks: ['pagoda', 'stonehenge', 'torii_gate'],
  },
  {
    id: 'city', name: 'City', order: 3, difficultyLabel: 'Medium', enabled: true, comingSoon: false,
    music: 'music_city', ambience: ['amb_traffic', 'amb_wind'], weather: 'clear',
    palette: { skyTop: '#6AA8F0', skyHorizon: '#E6ECF5', ground: '#9AA5B1', path: '#4A4F58', pathEdge: '#D8DDE3', accent: '#FFB703', fog: '#E3E8EF' },
    fogDensity: 0.008, decor: ['building', 'sidewalk', 'street_light', 'car', 'traffic_barrier', 'park_patch', 'tunnel'],
    landmarks: ['eiffel_tower', 'big_ben', 'statue_of_liberty'],
  },
  {
    id: 'mountains', name: 'Mountains', order: 4, difficultyLabel: 'Medium–Hard', enabled: true, comingSoon: false,
    music: 'music_mountains', ambience: ['amb_wind', 'amb_waterfall'], weather: 'snow',
    palette: { skyTop: '#3F86D9', skyHorizon: '#DCEBFA', ground: '#7C8E6A', path: '#B7A58A', pathEdge: '#8B7B64', accent: '#8ED1FC', fog: '#E9F2FB', water: '#6CC7F0' },
    fogDensity: 0.01, decor: ['mountain', 'cliff', 'waterfall', 'river', 'rope_bridge', 'boulder', 'snow_patch', 'cloud', 'pine'],
    landmarks: ['great_wall', 'christ_the_redeemer'],
  },
  {
    id: 'beach', name: 'Beach', order: 5, difficultyLabel: 'Hard', enabled: true, comingSoon: false,
    music: 'music_beach', ambience: ['amb_waves', 'amb_gulls'], weather: 'sea_breeze',
    palette: { skyTop: '#39B5F0', skyHorizon: '#FFF1D6', ground: '#F4DDA4', path: '#E9C98B', pathEdge: '#CFAE6F', accent: '#FF6F59', fog: '#FFF3E0', water: '#1FB5D6' },
    fogDensity: 0.005, decor: ['palm', 'ocean', 'boat', 'crab', 'sand_castle', 'wood_bridge', 'umbrella', 'shell'],
    landmarks: ['sydney_opera_house', 'little_mermaid', 'golden_gate_bridge'],
  },
  {
    id: 'snow', name: 'Snow World', order: 6, difficultyLabel: 'Hard', enabled: true, comingSoon: false,
    music: 'music_snow', ambience: ['amb_wind'], weather: 'snow',
    palette: { skyTop: '#7FB8F5', skyHorizon: '#EAF6FF', ground: '#F2F8FF', path: '#CFE3F2', pathEdge: '#9FC0DA', accent: '#5EC8F2', fog: '#EEF6FF', water: '#9ADCF5' },
    fogDensity: 0.009, decor: ['snow_pine', 'snowman', 'igloo', 'frozen_pond', 'sled', 'penguin', 'snow_patch', 'lamp_post'],
    landmarks: ['mount_fuji', 'st_basils_cathedral', 'santa_village'],
  },
  // Future worlds: the architecture already supports them — add levels that point at them and enable.
  ...([
    ['jungle', 'Jungle', 'music_jungle'], ['desert', 'Desert', 'music_desert'],
    ['fantasy', 'Fantasy World', 'music_fantasy'], ['candy', 'Candy World', 'music_candy'], ['sky_islands', 'Sky Islands', 'music_sky'],
    ['night_city', 'Night City', 'music_night_city'], ['space', 'Space World', 'music_space'],
  ] as const).map(([id, name, music], i): WorldDef => ({
    id, name, order: 7 + i, difficultyLabel: 'Coming soon', enabled: false, comingSoon: true,
    music, ambience: [], weather: 'clear',
    palette: { skyTop: '#7FA7FF', skyHorizon: '#F0E6FF', ground: '#C9B8F0', path: '#E7DDFB', pathEdge: '#B8A6E8', accent: '#FF8FD8', fog: '#F2ECFF' },
    fogDensity: 0.008, decor: [], landmarks: [],
  })),
];
