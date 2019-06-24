import { GeometryType } from '../header_generated'
import { Feature } from '../feature_generated'
import HeaderMeta from '../HeaderMeta'
import { createGeometryOl } from './geometry'
import { fromFeature as genericFromFeature, IFeature } from '../generic/feature'
import { ISimpleGeometry } from '../generic/geometry'

export function createFeatureOl(geometry: ISimpleGeometry, properties: any, ol: any): IFeature {
    const feature = new ol.Feature(geometry)
    if (properties)
        feature.setProperties(properties)
    return feature
}

export function fromFeature(feature: Feature, header: HeaderMeta, ol: any): IFeature {
    function createFeature(geometry: ISimpleGeometry, properties: any) {
        return createFeatureOl(geometry, properties, ol)
    }
    function createGeometry(feature: Feature, type: GeometryType) {
        return createGeometryOl(feature, type, ol)
    }
    return genericFromFeature(feature, header, createGeometry, createFeature)
}