export interface IFftRequest {
  fileId: string
}

export interface IFftBin {
  frequencyHz: number
  magnitude: number
}

export interface IFftResponse {
  bins: IFftBin[]
  fileId: string
  sourcePath: string
}
