export interface ITransformRecipe {
  gainDb: number
  normalize: boolean
  trimSilence: boolean
}

export const defaultTransformRecipe: ITransformRecipe = {
  gainDb: 0,
  normalize: false,
  trimSilence: false,
}

export const hasActiveTransforms = (recipe: ITransformRecipe): boolean =>
  recipe.normalize || recipe.trimSilence || Math.abs(recipe.gainDb) > 0.01

export const serializeTransformRecipe = (
  recipe: ITransformRecipe | null | undefined,
): string => {
  const value = recipe ?? defaultTransformRecipe

  return [value.normalize ? '1' : '0', value.trimSilence ? '1' : '0', value.gainDb.toFixed(2)].join(
    ':',
  )
}
