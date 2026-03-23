export type FilterMode = 'none' | 'lowpass' | 'highpass' | 'bandpass' | 'notch'

export interface IFilterRecipe {
  cutoffHz: number
  highCutoffHz: number
  lowCutoffHz: number
  mode: FilterMode
  q: number
}

export interface ITransformRecipe {
  filter: IFilterRecipe
  gainDb: number
  normalize: boolean
  trimSilence: boolean
}

export const defaultFilterRecipe: IFilterRecipe = {
  cutoffHz: 1200,
  highCutoffHz: 3500,
  lowCutoffHz: 250,
  mode: 'none',
  q: 0.707,
}

export const defaultTransformRecipe: ITransformRecipe = {
  filter: defaultFilterRecipe,
  gainDb: 0,
  normalize: false,
  trimSilence: false,
}

export const hasActiveTransforms = (recipe: ITransformRecipe): boolean =>
  recipe.normalize ||
  recipe.trimSilence ||
  Math.abs(recipe.gainDb) > 0.01 ||
  recipe.filter.mode !== 'none'

export const serializeTransformRecipe = (
  recipe: ITransformRecipe | null | undefined,
): string => {
  const value = recipe ?? defaultTransformRecipe

  return [
    value.normalize ? '1' : '0',
    value.trimSilence ? '1' : '0',
    value.gainDb.toFixed(2),
    value.filter.mode,
    value.filter.cutoffHz.toFixed(1),
    value.filter.lowCutoffHz.toFixed(1),
    value.filter.highCutoffHz.toFixed(1),
    value.filter.q.toFixed(3),
  ].join(':')
}
