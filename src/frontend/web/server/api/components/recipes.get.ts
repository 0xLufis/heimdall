import { defineEventHandler } from 'h3'
import { backendFetch } from '../../utils/backendFetch'

export default defineEventHandler((event) => backendFetch(event, 'component/recipes'))
