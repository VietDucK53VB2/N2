<template>
  <div>
    <v-card rounded="xl" elevation="1">
      <v-card-text>
        <!-- Stepper -->
        <v-stepper v-model="step" alt-labels class="elevation-0">
          <v-stepper-header>
            <v-stepper-item title="Nhập thông tin" :value="1" :complete="step > 1" />
            <v-divider />
            <v-stepper-item title="Xác nhận" :value="2" :complete="step > 2" />
            <v-divider />
            <v-stepper-item title="Hoàn tất" :value="3" />
          </v-stepper-header>

          <v-stepper-window>
            <!-- Step 1: Input -->
            <v-stepper-window-item :value="1">
              <v-row class="mt-4">
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="cardNum"
                    label="Mã thẻ độc giả"
                    prepend-inner-icon="mdi-card-account-details"
                    hide-details
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="bookId"
                    label="Mã sách (Book ID)"
                    prepend-inner-icon="mdi-book"
                    hide-details
                  />
                </v-col>
              </v-row>
              <div class="d-flex justify-end mt-6">
                <v-btn color="primary" size="large" :disabled="!cardNum || !bookId" @click="step = 2">
                  Tiếp tục <v-icon end>mdi-arrow-right</v-icon>
                </v-btn>
              </div>
            </v-stepper-window-item>

            <!-- Step 2: Confirm -->
            <v-stepper-window-item :value="2">
              <v-alert type="info" variant="tonal" class="mt-4 mb-4">
                <p class="mb-1"><strong>Mã thẻ:</strong> {{ cardNum }}</p>
                <p class="mb-0"><strong>Mã sách:</strong> {{ bookId }}</p>
              </v-alert>
              <div class="d-flex justify-space-between">
                <v-btn variant="text" @click="step = 1">
                  <v-icon start>mdi-arrow-left</v-icon> Quay lại
                </v-btn>
                <v-btn color="success" size="large" :loading="returning" @click="submitReturn">
                  <v-icon start>mdi-check</v-icon> Xác nhận trả sách
                </v-btn>
              </div>
            </v-stepper-window-item>

            <!-- Step 3: Done -->
            <v-stepper-window-item :value="3">
              <div class="text-center pa-8">
                <v-icon size="72" color="success" class="mb-4">mdi-check-circle</v-icon>
                <h3 class="text-h6 font-weight-bold mb-2">Trả sách thành công!</h3>
                <p v-if="fineAmount > 0" class="text-body-2 text-error">
                  Tiền phạt: <strong>{{ fineAmount.toLocaleString() }} VND</strong>
                </p>
                <p v-else class="text-body-2 text-success">Không có phí phạt</p>
                <v-btn color="primary" class="mt-4" @click="reset">
                  <v-icon start>mdi-plus</v-icon> Trả sách khác
                </v-btn>
              </div>
            </v-stepper-window-item>
          </v-stepper-window>
        </v-stepper>
      </v-card-text>
    </v-card>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { useLibrarianStore } from '@/stores/librarian'

const libStore = useLibrarianStore()
const step = ref(1)
const cardNum = ref('')
const bookId = ref('')
const returning = ref(false)
const fineAmount = ref(0)

async function submitReturn() {
  returning.value = true
  try {
    const r = await libStore.returnBook(cardNum.value, bookId.value)
    if (r.ok) {
      const data = await r.json()
      fineAmount.value = data.FineAmount || 0
      step.value = 3
    }
  } finally { returning.value = false }
}

function reset() {
  step.value = 1
  cardNum.value = ''
  bookId.value = ''
  fineAmount.value = 0
}
</script>
