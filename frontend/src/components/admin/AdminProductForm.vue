<script setup lang="ts">
/** Create/edit form for an administrator. Validation mirrors the API's FluentValidation rules. */
import { reactive, computed, watch } from 'vue'
import type { AdminCategory, AdminProduct, AdminProductRequest } from '@/types'

const props = withDefaults(
  defineProps<{
    product: AdminProduct | null
    categories: AdminCategory[]
    saving?: boolean
  }>(),
  { saving: false },
)

const emit = defineEmits<{
  submit: [payload: AdminProductRequest]
  cancel: []
}>()

interface FormState {
  name: string
  description: string
  price: number
  stock: number
  imageUrl: string
  discountPercent: number | null
  categoryId: number | null
  isFeatured: boolean
  isActive: boolean
}

function blank(): FormState {
  return {
    name: '',
    description: '',
    price: 999,
    stock: 10,
    imageUrl: '/images/products/placeholder.jpg',
    discountPercent: null,
    categoryId: props.categories[0]?.id ?? null,
    isFeatured: false,
    isActive: true,
  }
}

const form = reactive<FormState>(blank())

// The category list is fetched asynchronously by the page, so on a cold start this component can
// be constructed before any category exists. `blank()` would then leave categoryId null, `errors`
// would permanently contain "Choose a category", and the submit button would never enable.
// Adopt the first category as soon as one becomes available, for a new product only — an
// in-progress edit must keep the category it was loaded with.
watch(
  () => props.categories,
  (categories) => {
    if (!props.product && form.categoryId === null && categories.length > 0) {
      form.categoryId = categories[0].id
    }
  },
  { immediate: true },
)

// Load the product being edited, or reset to a blank form.
watch(
  () => props.product,
  (product) => {
    if (product) {
      Object.assign(form, {
        name: product.name,
        description: product.description ?? '',
        price: product.price,
        stock: product.stock,
        imageUrl: product.imageUrl,
        discountPercent: product.discountPercent,
        categoryId: product.categoryId,
        isFeatured: product.isFeatured,
        isActive: product.isActive,
      })
    } else {
      Object.assign(form, blank())
    }
  },
  { immediate: true },
)

const errors = computed(() => {
  const found: Record<string, string> = {}
  if (!form.name.trim()) found.name = 'Name is required'
  if (!(form.price > 0)) found.price = 'Price must be greater than 0'
  if (form.stock < 0) found.stock = 'Stock cannot be negative'
  if (form.discountPercent !== null && (form.discountPercent < 0 || form.discountPercent > 90)) {
    found.discountPercent = 'Discount must be between 0 and 90'
  }
  if (!form.categoryId) found.categoryId = 'Choose a category'
  return found
})

const valid = computed(() => Object.keys(errors.value).length === 0 && !props.saving)

function onSubmit(): void {
  if (!valid.value) return

  emit('submit', {
    name: form.name.trim(),
    slug: null, // The API derives a unique slug from the name.
    description: form.description.trim() || null,
    price: Number(form.price),
    stock: Number(form.stock),
    imageUrl: form.imageUrl.trim() || null,
    discountPercent: form.discountPercent,
    ratingNum: null,
    isFeatured: form.isFeatured,
    isActive: form.isActive,
    categoryId: Number(form.categoryId),
  })
}
</script>

<template>
  <form class="summary-card" style="position: static" novalidate @submit.prevent="onSubmit">
    <h2 class="h6 mb-3">{{ product ? `Edit “${product.name}”` : 'New product' }}</h2>

    <div class="row g-3">
      <div class="col-md-8">
        <div class="field">
          <label for="product-name">Name</label>
          <input id="product-name" v-model="form.name" type="text" />
          <span v-if="errors.name" class="field-error">{{ errors.name }}</span>
        </div>
      </div>

      <div class="col-md-4">
        <div class="field">
          <label for="product-category">Category</label>
          <select id="product-category" v-model="form.categoryId" class="form-select">
            <option :value="null" disabled>Choose…</option>
            <option v-for="c in categories" :key="c.id" :value="c.id">
              {{ c.parentName ? `${c.parentName} / ${c.name}` : c.name }}
            </option>
          </select>
          <span v-if="errors.categoryId" class="field-error">{{ errors.categoryId }}</span>
        </div>
      </div>

      <div class="col-12">
        <div class="field">
          <label for="product-description">Description</label>
          <textarea id="product-description" v-model="form.description" rows="3" />
        </div>
      </div>

      <div class="col-md-4">
        <div class="field">
          <label for="product-price">Price (₹)</label>
          <input id="product-price" v-model.number="form.price" type="number" min="0" step="0.01" />
          <span v-if="errors.price" class="field-error">{{ errors.price }}</span>
        </div>
      </div>

      <div class="col-md-4">
        <div class="field">
          <label for="product-stock">Stock</label>
          <input id="product-stock" v-model.number="form.stock" type="number" min="0" />
          <span v-if="errors.stock" class="field-error">{{ errors.stock }}</span>
        </div>
      </div>

      <div class="col-md-4">
        <div class="field">
          <label for="product-discount">Discount % (optional)</label>
          <input
            id="product-discount"
            v-model.number="form.discountPercent"
            type="number"
            min="0"
            max="90"
            placeholder="none"
          />
          <span v-if="errors.discountPercent" class="field-error">{{ errors.discountPercent }}</span>
        </div>
      </div>

      <div class="col-12">
        <div class="field">
          <label for="product-image">Image URL</label>
          <input id="product-image" v-model="form.imageUrl" type="text" />
        </div>
      </div>

      <div class="col-12 d-flex gap-4">
        <label class="d-flex align-items-center gap-2">
          <input v-model="form.isFeatured" type="checkbox" />
          <span class="small">Featured</span>
        </label>
        <label class="d-flex align-items-center gap-2">
          <input v-model="form.isActive" type="checkbox" />
          <span class="small">Active</span>
        </label>
      </div>
    </div>

    <div class="d-flex gap-2 mt-3">
      <button class="btn-pill btn-accent" type="submit" :disabled="!valid">
        {{ saving ? 'Saving…' : product ? 'Save changes' : 'Create product' }}
      </button>
      <button class="btn-pill btn-ghost" type="button" @click="emit('cancel')">Cancel</button>
    </div>
  </form>
</template>