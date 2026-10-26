- Diffuse/specular energy balance

- Right now Saga Standard gives the diffuse term the same share of light at every angle, but the specular terms share grows towards grazing angles. Near silhouettes the two shares add up to more than the light that came in

- When light hits a dielectric, it splits at the surface:
    - Reflected at the surface: a fraction F (the Fresnel reflectance) bounces off the surface like a mirror, this is Specular, it hasnt touched the materials pigment, so it keeps the lights color, thats why highlights on red plastic are white
    - Refracted into the material: The remaining 1 - F enters the material and scatters off pigment particles, some of it is absorbed, which is where the color comes from, whats left eventually exists in random directions, this is diffuse.

- Diffuse can only work with light the surface did not reflect, every photon goes one way or the other, so for a real surface: 

specular share + diffuse share <= 1

