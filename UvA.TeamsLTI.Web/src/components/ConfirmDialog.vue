<template>
<transition name="fade">
    <div class="modal-mask">
        <div class="modal-wrapper">
            <div class="modal-container">
                <h3><slot name="header">Delete team</slot></h3>
                <slot>Are you sure you want to delete this team? All conversations and files will be deleted, including any work uploaded by students or fellow lecturers.</slot>

                <div style="margin-top: 10px">
                  <label><input type="checkbox" v-model="isConfirmed" /> <slot name="confirm">Confirm deletion</slot></label>
                </div>

                <div style="margin-top: 20px">
                    <button class="button-primary" :disabled="!isConfirmed" @click="$emit('confirm')">Yes</button>
                    <button class="button-secondary" @click="$emit('close')">No</button>
                </div>
            </div>
        </div>
    </div>
</transition>
</template>

<script lang="ts">
import { Vue } from 'vue-class-component';

export default class ConfirmDialog extends Vue {
    isConfirmed = false;
}
</script>


<style lang="scss" scoped>
@use '../variables' as *;

.modal-mask {
  position: fixed;
  z-index: 9998;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background-color: rgba(0, 0, 0, 0.5);
  display: table;
  transition: opacity 0.3s ease;
}

h3 {
    margin-top: 0;
}

.modal-wrapper {
  display: table-cell;
  vertical-align: middle;
}

.modal-container {
  width: 240px;
  margin: 0px auto;
  padding: 25px 20px;
  background-color: #fff;
  border-radius: 10px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.33);
  transition: all 0.3s ease;
  overflow-y: auto;
}

.lds-dual-ring {
  display: inline-block;
  width: 70px;
  height: 80px;
}
.lds-dual-ring:after {
  content: " ";
  display: block;
  width: 50px;
  height: 50px;
  margin: 8px;
  border-radius: 50%;
  border: 6px solid $primary-color;
  border-color: $primary-color transparent $primary-color transparent;
  animation: lds-dual-ring 1.2s linear infinite;
}
@keyframes lds-dual-ring {
  0% {
    transform: rotate(0deg);
  }
  100% {
    transform: rotate(360deg);
  }
}

</style>